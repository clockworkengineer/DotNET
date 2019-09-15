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

        private const string CONTACTS_FILE = "./contacts.csv";
        private string _csvHeader = "Id,LastName,FirstName,EMail,PhoneNo";

        public ContactCSV()
        {
        }

        /// <summary>
        /// Writes a contact records to CSV file.
        /// </summary>
        public override void WriteContactRecord(ContactRecord contact)
        {
       
        }

        /// <summary>
        /// Delete a contact record from CSV file.
        /// </summary>
        public override void DeleteContactRecord(ContactRecord contact)
        {

        }

        /// <summary>
        /// Writes the contact records to CSV file.
        /// </summary>
        public override void FlushContactRecords(Dictionary<string, ContactRecord> contacts)
        {

            using (var writer = new StreamWriter(CONTACTS_FILE))
            {
                writer.WriteLine(_csvHeader);
                foreach (var id in contacts.Keys)
                {
                    var contact = contacts[id];
                    var line = contact.Id + "," + contact.FirstName + "," + contact.LastName + "," +
                    contact.EmailAddress + "," + contact.PhoneNumber;
                    writer.WriteLine(line);
                }
            }
        }

        /// <summary>
        /// Loads the contact records from CSV file.
        /// </summary>
        public override Dictionary<string, ContactRecord> LoadContactRecords()
        {

            var _contacts = new Dictionary<string, ContactRecord>();

            if (!File.Exists(CONTACTS_FILE))
            {
                FlushContactRecords(_contacts);
            }

            using (var reader = new StreamReader(CONTACTS_FILE))
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
                    contact.EmailAddress = values[3];
                    contact.PhoneNumber = values[4];
                    _contacts[contact.Id] = contact;
                }
            }

            return (_contacts);

        }
    }
}
