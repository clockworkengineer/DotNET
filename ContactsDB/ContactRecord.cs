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
        private string _id = String.Empty;          // Unique Integer ID
        private string _firstName = String.Empty;   // First Name
        private string _lastName = String.Empty;    // Last Name
        private string _phoneNo = String.Empty;     // Phone Number
        private string _email = String.Empty;       // E-mail address
        private string _comment = String.Empty;     // Comment

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ContactsDB.ContactRecord"/> class.
        /// </summary>
        public ContactRecord()
        {

        }

        public string FirstName { get => _firstName; set => _firstName = value; }
        public string LastName { get => _lastName; set => _lastName = value; }
        public string PhoneNo { get => _phoneNo; set => _phoneNo = value; }
        public string Email { get => _email; set => _email = value; }
        public string Id { get => _id; set => _id = value; }
        public string Comment { get => _comment; set => _comment = value; }
    }
}
