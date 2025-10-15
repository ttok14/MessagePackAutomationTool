using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack.Formatters;
using global::MessagePack;

namespace MSgPackBinaryGenerator
{
    namespace MessagePack.Resolvers
    {
        /// <summary>
        /// Singleton version of CompositeResolver, which be able to register a collection of formatters and resolvers to a single instance.
        /// </summary>
        public class MyCompositeResolver : IFormatterResolver
        {
            public static MyCompositeResolver Instance = new MyCompositeResolver();

            private bool freezed;
            private IReadOnlyList<IMessagePackFormatter> formatters;
            private IReadOnlyList<IFormatterResolver> resolvers;

            private MyCompositeResolver()
            {
                formatters = Array.Empty<IMessagePackFormatter>();
                resolvers = Array.Empty<IFormatterResolver>();
            }

            /// <summary>
            /// Initializes a singleton instance with the specified formatters and sub-resolvers.
            /// This method can only call before use StaticCompositeResolver.Instance.GetFormatter.
            /// If call twice in the Register methods, registered formatters and resolvers will be overridden.
            /// </summary>
            /// <param name="resolvers">
            /// A list of resolvers to use for serializing types.
            /// The resolvers are searched in the order given, so if two resolvers support serializing the same type, the first one is used.
            /// </param>
            public void Register(params IFormatterResolver[] resolvers)
            {
                if (this.freezed)
                {
                    throw new InvalidOperationException("Register must call on startup(before use GetFormatter<T>).");
                }

                if (resolvers is null)
                {
                    throw new ArgumentNullException(nameof(resolvers));
                }

                this.formatters = Array.Empty<IMessagePackFormatter>();
                this.resolvers = resolvers;
            }

            /// <summary>
            /// Gets an <see cref="IMessagePackFormatter{T}"/> instance that can serialize or deserialize some type <typeparamref name="T"/>.
            /// </summary>
            /// <typeparam name="T">The type of value to be serialized or deserialized.</typeparam>
            /// <returns>A formatter, if this resolver supplies one for type <typeparamref name="T"/>; otherwise <c>null</c>.</returns>
            public IMessagePackFormatter<T> GetFormatter<T>()
            {
                return Cache<T>.Formatter;
            }

            private static class Cache<T>
            {
                public static readonly IMessagePackFormatter<T> Formatter;

                static Cache()
                {
                    Instance.freezed = true;
                    foreach (var item in Instance.formatters)
                    {
                        if (item is IMessagePackFormatter<T> f)
                        {
                            Formatter = f;
                            return;
                        }
                    }

                    foreach (var item in Instance.resolvers)
                    {
                        var f = item.GetFormatter<T>();
                        if (f != null)
                        {
                            Formatter = f;
                            return;
                        }
                    }
                }
            }
        }
    }

}
