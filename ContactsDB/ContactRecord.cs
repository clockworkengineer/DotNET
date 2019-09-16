//
// Author: Robert Tizzard
//
// Program: Contacts Database.
//
// Description: Contact details record class.
//
// Copyright 2019.
//

using System;

namespace ContactsDB
{
    /// <summary>
    /// Contact record.
    /// </summary>
    public class ContactRecord
    {
        private string _id = String.Empty;              // Unique Integer ID
        private string _firstName = String.Empty;       // First Name
        private string _lastName = String.Empty;        // Last Name
        private string _phoneNumber = String.Empty;     // Phone Number
        private string _emailAddress = String.Empty;    // E-mail address

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ContactsDB.ContactRecord"/> class.
        /// </summary>
        public ContactRecord()
        {

        }

        public string FirstName { get => _firstName; set => _firstName = value; }
        public string LastName { get => _lastName; set => _lastName = value; }
        public string PhoneNumber { get => _phoneNumber; set => _phoneNumber = value; }
        public string EmailAddress { get => _emailAddress; set => _emailAddress = value; }
        public string Id { get => _id; set => _id = value; }
    }
}
