using System;
using System.Collections.Generic;

namespace ContactsDB
{
    public abstract class ContactDB
    {
        private Int32 _nextID = 0;

        public virtual int NextID { get => _nextID; set => _nextID = value; }

        public virtual void WriteContactRecord(ContactRecord contact)
        {

        }
        public virtual void DeleteContactRecord(ContactRecord contact)
        {

        }
        public virtual void FlushContactRecords(Dictionary<string, ContactRecord> contacts)
        {

        }
        public virtual Dictionary<string, ContactRecord> LoadContactRecords()
        {
            return (new Dictionary<string, ContactRecord>());
        }
    }
}
