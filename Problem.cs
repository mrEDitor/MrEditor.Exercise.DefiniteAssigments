// TODO:
// 1. add variable-length symbols list and support string.Format'ting messages with them?
// 2. add backling to underlying problematic statement?
public record Problem(string Message, string SymbolName)
{
    public const string ALREADY_DECLARED = "Symbol name already exists";

    public const string CAN_NOT_ASSIGN_TO_FUNCTION = "Can not assign value to function";

    public const string UNKNOWN_FUNCTION = "Unknown function called";

    public const string USED_THEN_DECLARED = "Variable can not be declared since is was used earlier and expected to be captured from external context";

    public const string VARIABLE_NOT_DECLARED = "No such variable declared";

    public const string VARIABLE_NOT_ASSIGNED = "Variable is not assigned";

}
