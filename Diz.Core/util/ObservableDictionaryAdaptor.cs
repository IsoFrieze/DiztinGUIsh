using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;

namespace Diz.Core.util
{
    // OdwWrapper is wrapping an issue we are having where ExtendedXmlSerializer is having issues
    // serializing ObservableDictionary<> correctly.  It's failing to cast to IDictionary, which is either
    // a problem with ObservableDictionary, or, a misconfiguration in the default config of ExtendedXmlSerializer
    //
    // Either way, the code in this file works around it (in kind of a clumsy way).  We should rip it out
    // as soon as we can.
    public static class OdWrapperRegistration
    {
        // tell ExtendedXmlSerializer NOT to serialize .Dict on OdWrapper
        public static IConfigurationContainer AppendDisablingType<TKey, TValue>(this IConfigurationContainer @this)
            => @this
                .EnableImplicitTyping(typeof(OdWrapper<TKey, TValue>))
                .Type<OdWrapper<TKey, TValue>>()
                .Member(x => x.Dict).Ignore(); // the important bit

        private static readonly List<Func<IConfigurationContainer, IConfigurationContainer>> operationFNs = new List<Func<IConfigurationContainer, IConfigurationContainer>>();

        // allow multiple OdWrapper type combos to be excluded.
        public static void Register<TKey, TValue>()
        {
            Func<IConfigurationContainer, IConfigurationContainer> fn = container =>
                container.AppendDisablingType<TKey, TValue>();

            operationFNs.Add(fn);
            Debug.Assert(operationFNs.Count != 0);
        }

        public static IConfigurationContainer ApplyAllOdWrapperConfigurations(this IConfigurationContainer @this) =>
            operationFNs.Aggregate(@this, (current, fn) => fn(current));
    }

    // wrapper around an ObservableDictionary so we can implement non-generic IDictionary
    // this basically exists to work around ExtendedXmlSerializer trying to cast us to IDictionary and failing.
    // there's probably settings we can tweak in ExtendedXmlSerializer (particularly, Interceptor), and then
    // we can remove the need for this wrapper.
    //
    // this entire mess is because no matter what I do I can't do (IDictionary)ObservableDictionary
    // which is what ExtendedXmlSerializer needs. this code all needs to GO
    public class OdWrapper<TKey, TValue>
    {
        // Real data we care about will live here in Dict.
        // The XML serializer will ignore Dict when saving/loading
        // The app code should use this directly.
        public ObservableDictionary<TKey, TValue> Dict { get; set; } = new ObservableDictionary<TKey,TValue>();

        private static bool _registered = false;

        // Expose a copy of Dict just for the XML serialization.
        // App code should NOT touch this except for XML save/load.
        public IDictionary DictToSave
        {
            get => new Dictionary<TKey, TValue>(Dict); // copy. potentially expensive.
            set
            {
                Dict.Clear();
                foreach (DictionaryEntry item in value) {
                    Dict.Add((TKey)item.Key, (TValue)item.Value);
                }
            }
        }
        public OdWrapper()
        {
            if (_registered) // reminder: this is per-<TKey/TValue> combo
                return;

            OdWrapperRegistration.Register<TKey, TValue>();
            _registered = true;
        }

        #region Equality

        protected bool Equals(OdWrapper<TKey, TValue> other)
        {
            return Dict.SequenceEqual(other.Dict);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OdWrapper<TKey, TValue>)obj);
        }

        public override int GetHashCode()
        {
            return Dict.GetHashCode();
        }

        #endregion
    }
}