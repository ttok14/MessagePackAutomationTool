using MessagePack;
using System;

namespace MSgPackBinaryGenerator
{
    /// <summary>
    /// 디버깅을 위해 다른 IFormatterResolver를 감싸고 GetFormatter 호출을 로깅하는 프록시 클래스
    /// </summary>
    public class LoggingResolver : IFormatterResolver
    {
        private readonly IFormatterResolver _innerResolver;
        private readonly string _resolverName;

        public LoggingResolver(IFormatterResolver innerResolver, string resolverName)
        {
            _innerResolver = innerResolver;
            _resolverName = resolverName;
        }

        public MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>()
        {
            Console.WriteLine($"[LOG] '{_resolverName}' is trying to get formatter for type: {typeof(T).FullName}");
            var formatter = _innerResolver.GetFormatter<T>();
            if (formatter != null)
            {
                Console.WriteLine($"[LOG] ==> '{_resolverName}' FOUND a formatter.");
            }
            else
            {
                Console.WriteLine($"[LOG] ==> '{_resolverName}' did NOT find a formatter.");
            }
            return formatter;
        }
    }
}
