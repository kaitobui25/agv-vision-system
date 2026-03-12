// Exposes internal members (e.g. ModbusClient.ParseRegisters) to the test project.
// This is the standard .NET pattern — kept in a dedicated file to avoid CS1529.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AgvControl.Tests")]
