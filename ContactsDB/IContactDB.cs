using System;
using System.Collections.Generic;

namespace ContactsDB
{
    public interface IContactDB
    {
        void WriteContactRecord(ContactRecord contact);
        void DeleteContactRecord(ContactRecord contact);
        void FlushContactRecords(Dictionary<string, ContactRecord> contacts);
        Dictionary<string, ContactRecord> LoadContactRecords();
    }
}
