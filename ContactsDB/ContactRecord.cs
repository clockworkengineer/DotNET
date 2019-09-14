﻿using System;
namespace ContactsDB
{
    public class ContactRecord
    {
        private string _id = String.Empty;
        private string _firstName = String.Empty;
        private string _lastName = String.Empty;
        private string _phoneNumber = String.Empty;
        private string _emailAddress = String.Empty;

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
