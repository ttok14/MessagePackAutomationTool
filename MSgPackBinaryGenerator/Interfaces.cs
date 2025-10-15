namespace MSgPackBinaryGenerator
{
    public interface IToSourceCode<T> where T : struct, System.Enum
    {
        string ToSourceCode(T type);
    }
}
