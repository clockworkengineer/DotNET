using System;

namespace ContactsDB
{

    public partial class ContactDBDialog : Gtk.Dialog
    {
        
        private ContactRecord _contact;

        public ContactDBDialog(ContactRecord contact, bool editable)
        {
            Build();

            if (contact.LastName != String.Empty)
            {
             //   lastNameEntry.IsEditable = false;
                lastNameEntry.Sensitive = false;
            }
            // firstNameEntry.IsEditable = editable;
            firstNameEntry.Sensitive = editable;
            //phoneNoEntry.IsEditable = editable;
            phoneNoEntry.Sensitive = editable;
            //emailEntry.IsEditable = editable;
            emailEntry.Sensitive = editable;
            //idEntry.IsEditable = false;
            idEntry.Sensitive = false;

            lastNameEntry.Text = contact.LastName;
            firstNameEntry.Text =  contact.FirstName;
            phoneNoEntry.Text = contact.PhoneNumber;
            emailEntry.Text = contact.EmailAddress;
            idEntry.Text = contact.Id;

            _contact = contact;

            this.ShowAll();
        }

        protected void OnButtonOkClicked(object sender, EventArgs e)
        {
            _contact.LastName = lastNameEntry.Text;
            _contact.FirstName = firstNameEntry.Text;
            _contact.EmailAddress = emailEntry.Text;
            _contact.PhoneNumber = phoneNoEntry.Text;
            this.Destroy();
        }

        protected void OnButtonCancelClicked(object sender, EventArgs e)
        {
            this.Destroy();
        }

    }
}
