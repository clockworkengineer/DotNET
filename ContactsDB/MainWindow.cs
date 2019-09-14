using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using ContactsDB;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    private ListStore _contactsDBStore;
    private TreeIter _selectedContact;
    private Dictionary<string, ContactRecord> _contacts;
    private Int32 _nextID=0;
    private const string CONTACTS_FILE = "./contacts.csv";
    private string _csvHeader = "Id,LastName,FirstName,EMail,PhoneNo";

    private void ButtonEnable(Button button, bool enabled)
    {
        if (enabled)
        {
            button.State = StateType.Normal;
            button.Sensitive = true;
        }
        else
        {
            button.State = StateType.Insensitive;
        }
    }

    private void SetButtonsStatus()
    {
        bool enable = (contactsDBTreeView.Selection.CountSelectedRows()>0);
        ButtonEnable(createButton, true);
        ButtonEnable(readButton, enable);
        ButtonEnable(updateButton, enable);
        ButtonEnable(deleteButton, enable);
    }

    private string GetSelectedKey()
    {
        return ((string)_contactsDBStore.GetValue(_selectedContact, 4));
    }

    private void AddContact(ContactRecord contact)
    {
        _contacts[contact.Id] = contact;
        _contactsDBStore.AppendValues(contact.LastName, contact.FirstName, 
                 contact.EmailAddress, contact.PhoneNumber, contact.Id);
    }

    private void RemoveSelectedContact()
    {
        _contacts.Remove(GetSelectedKey());
        _contactsDBStore.Remove(ref _selectedContact);
    }

    private void WriteContactRecords()
    {

        using (var writer = new StreamWriter(CONTACTS_FILE))
        {
            writer.WriteLine(_csvHeader);
            foreach (var id in _contacts.Keys)
            {
                var contact = _contacts[id];
                var line = contact.Id+","+contact.FirstName + "," + contact.LastName + "," + 
                contact.EmailAddress + "," + contact.PhoneNumber;
                writer.WriteLine(line);
            }
        }
    }

    private void LoadContactRecords()
    {

        _contacts = new Dictionary<string, ContactRecord>();

        if (!File.Exists(CONTACTS_FILE))
        {
            WriteContactRecords();
        }

        using (var reader = new StreamReader(CONTACTS_FILE))
        {

            _csvHeader = reader.ReadLine();
            while (!reader.EndOfStream)
            {
                var contact = new ContactRecord();
                var values = reader.ReadLine().Split(',');
                _nextID = Math.Max(Convert.ToInt32(values[0]), _nextID);
                contact.Id = values[0];
                contact.FirstName = values[1];
                contact.LastName = values[2];
                contact.EmailAddress = values[3];
                contact.PhoneNumber = values[4];
                AddContact(contact);
            }
        }
    }

    private void AddListViewColumn(string title, int cellNo)
    {
        TreeViewColumn column = new TreeViewColumn();
        column.Title = title;
        CellRendererText cell = new CellRendererText();
        column.PackStart(cell, true);
        contactsDBTreeView.AppendColumn(column);
        column.AddAttribute(cell, "text", cellNo);
    }
    
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        AddListViewColumn("Last Name", 0);
        AddListViewColumn("First Name", 1);
        AddListViewColumn("E-Mail", 2);
        AddListViewColumn("Phone No.", 3);
        AddListViewColumn("Id", 4);

        _contactsDBStore = new ListStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string));
        contactsDBTreeView.Model = _contactsDBStore;
        _contactsDBStore.SetSortColumnId(0, SortType.Ascending);

        LoadContactRecords();

        if (_contacts.Count > 0)
        {
            if (_contactsDBStore.GetIterFirst(out _selectedContact))
            {
                contactsDBTreeView.Selection.SelectIter(_selectedContact);
            }

        }

        SetButtonsStatus();

    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    protected void OnCreateButtonClicked(object sender, EventArgs e)
    {
        ContactRecord contact = new ContactRecord();
        contact.Id = (_nextID + 1).ToString();
        ContactDBDialog dialog = new ContactDBDialog(contact, true);
        if ((ResponseType)dialog.Run() == ResponseType.Ok)
        {
            _nextID++;
            AddContact(contact);
            WriteContactRecords();
        }
        SetButtonsStatus();
    }

    protected void OnReadButtonClicked(object sender, EventArgs e)
    {
        ContactDBDialog dialog = new ContactDBDialog(_contacts[GetSelectedKey()], false);
        dialog.Run();
        SetButtonsStatus();
    }

    protected void OnUpdateButtonClicked(object sender, EventArgs e)
    {
        ContactRecord contact = _contacts[GetSelectedKey()];

        ContactDBDialog dialog = new ContactDBDialog(contact, true);
        if ((ResponseType)dialog.Run() == ResponseType.Ok)
        {
            _contactsDBStore.SetValues(_selectedContact, contact.LastName, contact.FirstName,
                                            contact.EmailAddress, contact.PhoneNumber);
            WriteContactRecords();
        }

        SetButtonsStatus();

    }

    protected void OnDeleteButtonClicked(object sender, EventArgs e)
    {

        RemoveSelectedContact();
        WriteContactRecords();
        if (contactsDBTreeView.Selection.CountSelectedRows() > 0) {
            contactsDBTreeView.Selection.SelectIter(_selectedContact);
        }
        SetButtonsStatus();

    }

    protected void OnContactsDBTreeViewCursorChanged(object sender, EventArgs e)
    {
        contactsDBTreeView.Selection.GetSelected(out _selectedContact);
        SetButtonsStatus();
    }

}
