//
// Program: Contacts Database.
//
// Description: Contact details database store SQLite implementation.
//
// Copyright 2019.
//

using System;
using Mono.Data.Sqlite;
using System.Collections.Generic;

namespace ContactsDB
{
    public class ContactSQLite : ContactDBStore
    {
        private string sqlConnectionString = "URI=file:";
        private const string sqlUpdateContact = "UPDATE contacts SET FirstName=@FirstName,LastName=@LastName,PhoneNo=@PhoneNo,Email=@Email WHERE Id = @Id";
        private const string sqlDeleteContact = "DELETE FROM contacts WHERE Id = @Id";
        private const string sqlCreateContact = "INSERT INTO Contacts(FirstName,Lastname,PhoneNo,EMail) VALUES(@FirstName, @LastName, @PhoneNo, @Email)";
        private const string sqlReadAllContacts = "SELECT Id,FirstName,Lastname,PhoneNo,EMail FROM contacts;";
        private const string sqlCreateContactsTable = "CREATE TABLE IF NOT EXISTS contacts (Id INTEGER PRIMARY KEY, LastName TEXT, FirstName TEXT, PhoneNo TEXT, EMail TEXT)";

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ContactsDB.ContactSQLite"/> class.
        /// </summary>
        public ContactSQLite(string fileName)
        {
            sqlConnectionString += fileName;

            using (var dbConnection = new SqliteConnection(sqlConnectionString))
            {
                dbConnection.Open();
                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = sqlCreateContactsTable;
                    dbCommand.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Updates a contact record.
        /// </summary>
        /// <param name="contact">Contact.</param>
        public override void UpdateContactRecord(ContactRecord contact)
        {
            using (var dbConnection = new SqliteConnection(sqlConnectionString))
            {
                dbConnection.Open();
                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = sqlUpdateContact;
                    dbCommand.Prepare();
                    dbCommand.Parameters.AddWithValue("@FirstName", contact.FirstName);
                    dbCommand.Parameters.AddWithValue("@LastName", contact.LastName);
                    dbCommand.Parameters.AddWithValue("@PhoneNo", contact.PhoneNo);
                    dbCommand.Parameters.AddWithValue("@Email", contact.Email);
                    dbCommand.Parameters.AddWithValue("@Id", contact.Id);
                    dbCommand.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Writes a contact record.
        /// </summary>
        /// <param name="contact">Contact.</param>
        public override void WriteContactRecord(ContactRecord contact)
        {
            using (var dbConnection = new SqliteConnection(sqlConnectionString))
            {
                dbConnection.Open();
                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = sqlCreateContact;
                    dbCommand.Prepare();
                    dbCommand.Parameters.AddWithValue("@FirstName", contact.FirstName);
                    dbCommand.Parameters.AddWithValue("@LastName", contact.LastName);
                    dbCommand.Parameters.AddWithValue("@PhoneNo", contact.PhoneNo);
                    dbCommand.Parameters.AddWithValue("@Email", contact.Email);
                    dbCommand.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Deletes a contact record.
        /// </summary>
        /// <param name="contact">Contact.</param>
        public override void DeleteContactRecord(ContactRecord contact)
        {
            using (var dbConnection = new SqliteConnection(sqlConnectionString))
            {
                dbConnection.Open();
                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = sqlDeleteContact;
                    dbCommand.Prepare();
                    dbCommand.Parameters.AddWithValue("@Id", contact.Id);
                    dbCommand.ExecuteNonQuery();
                }
            }

        }

        /// <summary>
        /// Loads the contact records from permanent store.
        /// </summary>
        /// <returns>The contact records.</returns>
        public override Dictionary<string, ContactRecord> LoadContactRecords()
        {
            var contacts = new Dictionary<string, ContactRecord>();
        
            using (var dbConnection = new SqliteConnection(sqlConnectionString)) {
                dbConnection.Open();
                using (var dbCommand = dbConnection.CreateCommand())
                {
                    dbCommand.CommandText = sqlReadAllContacts; ;
                    using (var dbReader = dbCommand.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            var contact = new ContactRecord();
                            contact.Id = dbReader.GetInt32(0).ToString();
                            contact.FirstName = dbReader.GetString(1);
                            contact.LastName = dbReader.GetString(2);
                            contact.PhoneNo = dbReader.GetString(3);
                            contact.Email = dbReader.GetString(4);
                            contacts[contact.Id] = contact;
                            NextID = Math.Max(Convert.ToInt32(contact.Id), NextID);
                        }
                    }
                }
            }
            return (contacts);
        }
    }
}
