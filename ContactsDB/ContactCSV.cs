//
// Author: Robert Tizzard
//
// Program: Contacts Database.
//
// Description: Contact details store CSV file implementation.
//
// Copyright 2019.
//

using System;
using System.IO;
using System.Collections.Generic;

namespace ContactsDB
{
    /// <summary>
    /// Contact store CSV implementation.
    /// </summary>
    public class ContactCSV : ContactDB
    {

        private  string _contactsFileName;
        private string _csvHeader = "Id,LastName,FirstName,EMail,PhoneNo";

        public ContactCSV(string fileName)
        {
            _contactsFileName = fileName;
        }

        /// <summary>
        /// Writes the contact records to CSV file.
        /// </summary>
        public override void FlushContactRecords(Dictionary<string, ContactRecord> contacts)
        {

            using (var writer = new StreamWriter(_contactsFileName))
            {
                writer.WriteLine(_csvHeader);
                foreach (var id in contacts.Keys)
                {
                    var contact = contacts[id];
                    writer.WriteLine($"{contact.Id},{contact.FirstName},{contact.LastName},{contact.Email},{contact.PhoneNo}");
                }
            }
        }

        /// <summary>
        /// Loads the contact records from CSV file.
        /// </summary>
        public override Dictionary<string, ContactRecord> LoadContactRecords()
        {

            var _contacts = new Dictionary<string, ContactRecord>();

            if (!File.Exists(_contactsFileName))
            {
                FlushContactRecords(_contacts);
            }

            using (var reader = new StreamReader(_contactsFileName))
            {

                _csvHeader = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var contact = new ContactRecord();
                    var values = reader.ReadLine().Split(',');
                    NextID = Math.Max(Convert.ToInt32(values[0]), NextID);
                    contact.Id = values[0];
                    contact.FirstName = values[1];
                    contact.LastName = values[2];
                    contact.Email = values[3];
                    contact.PhoneNo = values[4];
                    _contacts[contact.Id] = contact;
                }
            }

            return (_contacts);

        }
    }
}
