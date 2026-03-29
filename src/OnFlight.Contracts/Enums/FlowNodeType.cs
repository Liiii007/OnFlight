namespace OnFlight.Contracts.Enums;

public enum FlowNodeType
{
    Task = 0,
    Loop = 1,
    Fork = 2,
    Join = 3,
    Branch = 4,
    ConsoleExecute = 5
}
