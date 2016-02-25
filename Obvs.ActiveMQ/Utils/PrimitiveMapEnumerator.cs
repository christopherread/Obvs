using System.Collections;
using Apache.NMS;

namespace Obvs.ActiveMQ.Utils
{
    /// <summary>
    /// Allows enumerating Apache.NMS.IPrimitiveMap as an IDictionaryEnumerator
    /// </summary>
    internal class PrimitiveMapEnumerator : IDictionaryEnumerator
    {
        private readonly IEnumerator _values;
        private readonly IEnumerator _keys;

        public PrimitiveMapEnumerator(IPrimitiveMap original)
        {
            _values = original.Values.GetEnumerator();
            _keys = original.Keys.GetEnumerator();
        }

        public bool MoveNext()
        {
            return _keys.MoveNext() && _values.MoveNext();
        }

        public void Reset()
        {
            _keys.Reset();
            _values.Reset();
        }

        public object Current { get { return Entry; } }
        public object Key { get {return _keys.Current;} }
        public object Value { get { return _values.Current; } }
        public DictionaryEntry Entry { get { return new DictionaryEntry(_keys.Current, _values.Current); } }
    }
}