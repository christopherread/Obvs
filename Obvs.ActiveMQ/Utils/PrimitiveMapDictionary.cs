using System;
using System.Collections;
using Apache.NMS;

namespace Obvs.ActiveMQ.Utils
{
    /// <summary>
    /// Apache.NMS.IPrimitiveMap dictonary adapter implementation
    /// Avoids leaking IPrimitiveMap dependency and avoids copying to new Dictionary
    /// </summary>
    internal class PrimitiveMapDictionary : IDictionary
    {
        private readonly IPrimitiveMap _original;

        public PrimitiveMapDictionary(IPrimitiveMap original)
        {
            _original = original;
        }

        public bool Contains(object key)
        {
            return _original.Contains(key);
        }

        public void Add(object key, object value)
        {
            _original[key.ToString()] = value;
        }

        public void Clear()
        {
            _original.Clear();
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return new PrimitiveMapEnumerator(_original);
        }

        public void Remove(object key)
        {
            _original.Remove(key);
        }

        public object this[object key]
        {
            get { return _original[key.ToString()]; }
            set { _original[key.ToString()] = value; }
        }

        public ICollection Keys
        {
            get { return _original.Keys; }
        }

        public ICollection Values
        {
            get { return _original.Values; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            int i = 0;
            foreach (var value in _original.Values)
            {
                if (i >= index)
                {
                    array.SetValue(value, i-index);
                }
                i++;
            }
        }

        public int Count
        {
            get { return _original.Count; }
        }

        public object SyncRoot
        {
            get { return _original; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }
    }
}