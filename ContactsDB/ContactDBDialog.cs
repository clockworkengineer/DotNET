using System;

namespace ContactsDB
{
    /// <summary>
    /// Contact dialog used for create/read/update/delete.
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

            if (contact.LastName != String.Empty)
            {
                lastNameEntry.Sensitive = false;
            }
            firstNameEntry.Sensitive = editable;
            phoneNoEntry.Sensitive = editable;
            emailEntry.Sensitive = editable;
            idEntry.Sensitive = false;

            lastNameEntry.Text = contact.LastName;
            firstNameEntry.Text =  contact.FirstName;
            phoneNoEntry.Text = contact.PhoneNumber;
            emailEntry.Text = contact.EmailAddress;
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
            _contact.LastName = lastNameEntry.Text;
            _contact.FirstName = firstNameEntry.Text;
            _contact.EmailAddress = emailEntry.Text;
            _contact.PhoneNumber = phoneNoEntry.Text;
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
