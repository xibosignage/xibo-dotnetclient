using System.Collections.Generic;
using System.Linq;

namespace XiboClient.Log
{
    /// <summary>
    /// A buffer for holding log messages
    /// With thanks: https://codereview.stackexchange.com/a/135565
    /// </summary>
    public sealed class ConcurrentCircularBuffer
    {
        private readonly LinkedList<LogMessage> _buffer;
        private int _maxItemCount;

        public ConcurrentCircularBuffer(int maxItemCount)
        {
            _maxItemCount = maxItemCount;
            _buffer = new LinkedList<LogMessage>();
        }

        public void Put(LogMessage item)
        {
            lock (_buffer)
            {
                _buffer.AddFirst(item);
                if (_buffer.Count > _maxItemCount)
                {
                    _buffer.RemoveLast();
                }
            }
        }

        public IEnumerable<LogMessage> Read()
        {
            lock (_buffer) { return _buffer.ToArray(); }
        }
    }
}
