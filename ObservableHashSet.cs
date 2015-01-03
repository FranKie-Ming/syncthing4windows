using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syncthing4Windows
{
    class ObservableHashSet<T> : HashSet<T>, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public bool Add(T obj) {
            var outcome = base.Add(obj);
            if (outcome)
                CollectionChanged(this, null);
            return outcome;
        }

        public bool Remove(T obj)
        {
            var outcome = base.Remove(obj);
            if (outcome)
                CollectionChanged(this, null);
            return outcome;
        }
    }
}
