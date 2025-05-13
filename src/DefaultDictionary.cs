using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DocExtractor
{
    /// <summary>
    /// A dictionary that can use a delegate to produce a value when asked
    /// for a key that isn't stored in the dictionary.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <inheritdoc/>
    internal class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _storage = new Dictionary<TKey, TValue>();

        public delegate TValue FallbackValueProvider(TKey key);
        public delegate TKey AlternativeKeyProvider(TKey key);

        private FallbackValueProvider fallbackProvider;
        private AlternativeKeyProvider alternativeKeyProvider;

        /// <summary>
        /// Creates a new instance of <see
        /// cref="DefaultDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="fallbackProvider">The method to use when the
        /// dictionary is asked for a key that is not present, and if
        /// <paramref name="alternativeKeyProvider"/> returns <see langword="null"/>
        /// or is not provided.</param>
        /// <param name="alternativeKeyProvider">A method to use to provide an
        /// alternate key, if the dictionary is asked for a key that is not
        /// present.</param>
        public DefaultDictionary(FallbackValueProvider fallbackProvider, AlternativeKeyProvider alternativeKeyProvider = null)
        {
            this.fallbackProvider = fallbackProvider;
            this.alternativeKeyProvider = alternativeKeyProvider;
        }

        /// <summary>
        /// Creates a new instance of <see
        /// cref="DefaultDictionary{TKey,TValue}"/> that uses another
        /// dictionary as its underlying storage.
        /// </summary>
        /// <param name="fallbackProvider"><inheritdoc cref="DefaultDictionary(FallbackValueProvider)" path="/param[@name='valueFactory']/node()" /></param>
        /// <param name="sourceDictionary"></param>
        public DefaultDictionary(Dictionary<TKey, TValue> sourceDictionary, FallbackValueProvider fallbackProvider, AlternativeKeyProvider alternativeKeyProvider = null)
        {
            this.fallbackProvider = fallbackProvider;
            this.alternativeKeyProvider = alternativeKeyProvider;
            _storage = sourceDictionary;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (_storage.ContainsKey(key))
                {
                    return ((IDictionary<TKey, TValue>)_storage)[key];
                }

                if (alternativeKeyProvider != null)
                {
                    // Try and get an alternative key.
                    var alternateKey = alternativeKeyProvider(key);
                    if (alternateKey != null)
                    {
                        key = alternateKey;
                    }

                    // If we got one, try and fetch _that_ from the dictionary.
                    if (_storage.ContainsKey(key))
                    {
                        // If we contain a value for the alternate key, use that
                        // directly
                        return ((IDictionary<TKey, TValue>)_storage)[key];
                    }
                    // Otherwise, proceed to using the fallback provider
                }

                // Get a value for this key from the fallback provider.
                if (fallbackProvider != null)
                {
                    return fallbackProvider(key);
                }
                else
                {
                    // We tried everything we could! Throw a 'not found'
                    // exception.
                    throw new KeyNotFoundException(key.ToString());
                }
            }

            set => ((IDictionary<TKey, TValue>)_storage)[key] = value;
        }

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)_storage).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)_storage).Values;

        public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)_storage).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)_storage).IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            ((IDictionary<TKey, TValue>)_storage).Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_storage).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_storage).Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_storage).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            // Return true if we have a value for this key; if it doesn't, and
            // we have an alternative key provider, return true if we have a
            // value for the alternative key.
            return ((IDictionary<TKey, TValue>)_storage).ContainsKey(key) || (alternativeKeyProvider != null && _storage.ContainsKey(alternativeKeyProvider(key)));
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_storage).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)_storage).GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            return ((IDictionary<TKey, TValue>)_storage).Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_storage).Remove(item);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return ((IDictionary<TKey, TValue>)_storage).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_storage).GetEnumerator();
        }
    }

}
