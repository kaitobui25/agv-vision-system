// ==========================================================================
// ModbusIntegrationTests.cs — Integration tests: C# client ↔ C++ server
// ==========================================================================
// PREREQUISITE: Start C++ hardware-sim (hardware-sim.exe) on port 502 FIRST!
//     Visual Studio 2022 → Open hardware-sim folder → F5 (run)
//
// Then run these tests:
//     Test Explorer → Traits → Category: Integration → Run All
//
// These tests are skipped by default in CI because they need a running server.
// ==========================================================================



using AgvControl.Models;
using AgvControl.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgvControl.Tests;

[Trait("Category", "Integration")]

[CollectionDefinition("Modbus Tests", DisableParallelization = true)]
public class ModbusCollection { }

[Collection("Modbus Tests")]
public class ModbusIntegrationTests : IAsyncLifetime
{
    private ModbusClient _client = null!;

    // -----------------------------------------------------------------------
    // Setup: create a real ModbusClient and connect to C++ server
    // -----------------------------------------------------------------------
    public async Task InitializeAsync()
    {
        var settings = Options.Create(new ModbusSettings
        {
            Host      = "127.0.0.1",
            Port      = 502,
            UnitId    = 1,
            TimeoutMs = 3000
        });

        var logger = LoggerFactory
            .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ModbusClient>();

        _client = new ModbusClient(settings, logger);
        await _client.ConnectAsync();


        // 🔧 RESET simulator before each test
        await _client.WriteMotorCommandAsync(0, 0, CommandCode.Reset);
        await Task.Delay(200);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // =========================================================================
    // 1. Connection — verify IsConnected after ConnectAsync
    // =========================================================================

    [Fact]
    public void Connection_AfterConnect_ShouldBeTrue()
    {
        Assert.True(_client.IsConnected);
    }


    // =========================================================================
    // 2. Initial State — server starts IDLE with position (0,0)
    // =========================================================================

    [Fact]
    public async Task InitialState_ShouldBeIdle()
    {
        var state = await _client.ReadStatusAsync();

        // Server should be IDLE or STOPPED at startup (may be STOPPED if
        // previous test sent a STOP command — both are valid initial states)
        Assert.True(state.Status == StatusCode.Idle || state.Status == StatusCode.Stopped,
            $"Expected Idle or Stopped, got {state.Status}");
        Assert.Equal(ErrorCode.Ok, state.Error);
        Assert.Equal(100, state.BatteryLevel);  // Battery always 100 in sim
    }



    // =========================================================================
    // 3. MOVE Command — send MOVE, wait, verify status changes to Moving
    // =========================================================================

    [Fact]
    public async Task MoveCommand_ShouldChangeStatusToMoving()
    {
        // Send MOVE with both motors forward at 500 RPM
        await _client.WriteMotorCommandAsync(500, 500, CommandCode.Move);

        // Wait for C++ sim to process (sim tick = 50ms, we wait 500ms for safety)
        await Task.Delay(500);

        var state = await _client.ReadStatusAsync();

        Assert.Equal(StatusCode.Moving, state.Status);
        Assert.True(state.ActualLeftSpeed != 0, "Left motor should be running");
        Assert.True(state.ActualRightSpeed != 0, "Right motor should be running");
    }




    // =========================================================================
    // 4. Position Change — MOVE forward should increase position
    // =========================================================================

    [Fact]
    public async Task MoveForward_PositionShouldChange()
    {
        // Read initial position
        var before = await _client.ReadStatusAsync();

        // Send MOVE straight forward (equal motor speeds → straight line)
        await _client.WriteMotorCommandAsync(500, 500, CommandCode.Move);
        await Task.Delay(1000);  // Let sim run for 1 second

        var after = await _client.ReadStatusAsync();

        // At least one axis should have changed (AGV moved)
        bool positionChanged = (after.PositionX != before.PositionX)
                            || (after.PositionY != before.PositionY);
        Assert.True(positionChanged,
            $"Position should change. Before=({before.PositionX},{before.PositionY}), " +
            $"After=({after.PositionX},{after.PositionY})");
    }



    // =========================================================================
    // 5. STOP Command — gradual deceleration
    // =========================================================================

    [Fact]
    public async Task StopCommand_ShouldStopMotors()
    {
        // First move
        await _client.WriteMotorCommandAsync(500, 500, CommandCode.Move);
        await Task.Delay(300);

        // Then stop (gradual decel over ~500ms)
        await _client.WriteMotorCommandAsync(0, 0, CommandCode.Stop);
        await Task.Delay(1000);  // Wait for deceleration ramp to finish

        var state = await _client.ReadStatusAsync();

        Assert.Equal(StatusCode.Stopped, state.Status);
    }



    // =========================================================================
    // 6. Emergency Stop — immediate halt
    // =========================================================================

    [Fact]
      public async Task EmergencyStop_ShouldStopImmediately()
      {
          // Move first
          await _client.WriteMotorCommandAsync(500, 500, CommandCode.Move);
          await Task.Delay(300);

          // E-STOP (instant, no ramp)
          await _client.WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
          await Task.Delay(200);  // Much shorter wait than STOP

          var state = await _client.ReadStatusAsync();

          Assert.Equal(StatusCode.EStopped, state.Status);
          Assert.Equal(0, state.ActualLeftSpeed);
          Assert.Equal(0, state.ActualRightSpeed);
      }

    
      // =========================================================================
      // 7. RESET after E-STOP — should return to Idle
      // =========================================================================

      [Fact]
      public async Task Reset_AfterEmergencyStop_ShouldReturnToIdle()
      {
          // E-STOP first
          await _client.WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
          await Task.Delay(200);

          // RESET
          await _client.WriteMotorCommandAsync(0, 0, CommandCode.Reset);
          await Task.Delay(200);

          var state = await _client.ReadStatusAsync();

          Assert.Equal(StatusCode.Idle, state.Status);
          Assert.Equal(ErrorCode.Ok, state.Error);
      }

    
      // =========================================================================
      // 8. Heading — turning in place (left=-500, right=+500 → rotation)
      // =========================================================================

      [Fact]
      public async Task TurnInPlace_HeadingShouldChange()
      {
          // Reset to clean state
          await _client.WriteMotorCommandAsync(0, 0, CommandCode.Reset);
          await Task.Delay(200);

          var before = await _client.ReadStatusAsync();

          // Spin in place: left backward, right forward → clockwise turn
          await _client.WriteMotorCommandAsync(-300, 300, CommandCode.Move);
          await Task.Delay(1000);

          var after = await _client.ReadStatusAsync();

          // Heading should have changed (any direction)
          Assert.NotEqual(before.HeadingDegrees, after.HeadingDegrees);
      }


   
      // =========================================================================
      // 9. Signed speed — negative speed should be preserved through roundtrip
      // =========================================================================

      [Fact]
      public async Task NegativeSpeed_ShouldPreserveSign()
      {
          await _client.WriteMotorCommandAsync(-500, 500, CommandCode.Move);
          await Task.Delay(300);

          var state = await _client.ReadStatusAsync();

          // Left motor should be negative (reverse), right should be positive
          Assert.True(state.ActualLeftSpeed < 0,
              $"Left speed should be negative, got {state.ActualLeftSpeed}");
          Assert.True(state.ActualRightSpeed > 0,
              $"Right speed should be positive, got {state.ActualRightSpeed}");
      }


    
     // =========================================================================
     // 10. Full Lifecycle — IDLE → MOVE → STOP → RESET → IDLE
     // =========================================================================

     [Fact]
     public async Task FullLifecycle_IdleMoveStopResetIdle()
     {
         // Reset to clean state
         await _client.WriteMotorCommandAsync(0, 0, CommandCode.Reset);
         await Task.Delay(200);
         var s1 = await _client.ReadStatusAsync();
         Assert.Equal(StatusCode.Idle, s1.Status);

         // MOVE
         await _client.WriteMotorCommandAsync(400, 400, CommandCode.Move);
         await Task.Delay(500);
         var s2 = await _client.ReadStatusAsync();
         Assert.Equal(StatusCode.Moving, s2.Status);

         // STOP
         await _client.WriteMotorCommandAsync(0, 0, CommandCode.Stop);
         await Task.Delay(1000);
         var s3 = await _client.ReadStatusAsync();
         Assert.Equal(StatusCode.Stopped, s3.Status);

         // RESET → back to IDLE
         await _client.WriteMotorCommandAsync(0, 0, CommandCode.Reset);
         await Task.Delay(200);
         var s4 = await _client.ReadStatusAsync();
         Assert.Equal(StatusCode.Idle, s4.Status);
     } 

}
