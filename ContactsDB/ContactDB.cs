//
// Author: Robert Tizzard
//
// Program: Contacts Database.
//
// Description: Contact details store implementation abtract class.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;

namespace ContactsDB
{
    public abstract class ContactDB
    {
        private Int32 _nextID = 0;

        public virtual int NextID { get => _nextID; set => _nextID = value; }

        /// <summary>
        /// Writes a contact record.
        /// </summary>
        /// <param name="contact">Contact.</param>
        public virtual void WriteContactRecord(ContactRecord contact)
        {

        }

        /// <summary>
        /// Deletes a contact record.
        /// </summary>
        /// <param name="contact">Contact.</param>
        public virtual void DeleteContactRecord(ContactRecord contact)
        {

        }

        /// <summary>
        /// Flushs all contact records to permanent store.
        /// </summary>
        /// <param name="contacts">Contacts.</param>
        public virtual void FlushContactRecords(Dictionary<string, ContactRecord> contacts)
        {

        }

        /// <summary>
        /// Loads the contact records from permanent store.
        /// </summary>
        /// <returns>The contact records.</returns>
        public virtual Dictionary<string, ContactRecord> LoadContactRecords()
        {
            return (new Dictionary<string, ContactRecord>());
        }

    }
}
