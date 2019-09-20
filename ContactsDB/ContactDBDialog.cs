//
// Author: Robert Tizzard
//
// Program: Contacts Database.
//
// Description: Contact details entry and display dialog.
//
// Copyright 2019.
//

using System;

namespace ContactsDB
{
    /// <summary>
    /// Contact dialog used for create/read/update.
    /// </summary>
    public partial class ContactDBDialog : Gtk.Dialog
    {
        
        private ContactRecord _contact;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ContactsDB.ContactDBDialog"/> class.
        /// </summary>
        /// <param name="contact">Contact.</param>
        /// <param name="editable">If set to <c>true</c> editable.</param>
        public ContactDBDialog(ContactRecord contact, bool editable)
        {
            Build();

            // For non empty contact record lock last name from change and lock others
            // depending if its an update or read.

            if (contact.LastName != String.Empty)
            {
                lastNameEntry.Sensitive = false;
            }
            firstNameEntry.Sensitive = editable;
            phoneNoEntry.Sensitive = editable;
            emailEntry.Sensitive = editable;
            idEntry.Sensitive = false;

            // Copy contact detailt to dialog and display

            lastNameEntry.Text = contact.LastName;
            firstNameEntry.Text =  contact.FirstName;
            phoneNoEntry.Text = contact.PhoneNo;
            emailEntry.Text = contact.Email;          
            idEntry.Text = contact.Id;

            _contact = contact;
            
            this.ShowAll();

        }

        /// <summary>
        /// On the button ok clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnButtonOkClicked(object sender, EventArgs e)
        {
            // Copy modified details back to record

            _contact.LastName = lastNameEntry.Text;
            _contact.FirstName = firstNameEntry.Text;
            _contact.Email = emailEntry.Text;
            _contact.PhoneNo = phoneNoEntry.Text;

            // Dispose of dialog resources

            this.Destroy();

        }

        /// <summary>
        /// On the button cancel clicked.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnButtonCancelClicked(object sender, EventArgs e)
        {
            this.Destroy();
        }

    }
}
