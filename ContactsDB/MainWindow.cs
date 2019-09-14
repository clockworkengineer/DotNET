using System;
using System.Collections.Generic;
using System.IO;
using ContactsDB;
using Gtk;

/// <summary>
/// Main window class for contacts database program.
/// </summary>
public partial class MainWindow : Gtk.Window
{

    private ListStore _contactsDBStore;
    private TreeIter _selectedContact;
    private Dictionary<string, ContactRecord> _contacts;
    private Int32 _nextID=0;
    private const string CONTACTS_FILE = "./contacts.csv";
    private string _csvHeader = "Id,LastName,FirstName,EMail,PhoneNo";

    /// <summary>
    /// Enable/Disable a window button.
    /// </summary>
    /// <param name="button">Button.</param>
    /// <param name="enabled">If set to <c>true</c> enable otherwise disabled.</param>
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

    /// <summary>
    /// Sets the buttons status of create/read/update/delete buttons.
    /// </summary>
    private void SetButtonsStatus()
    {
        bool enable = (contactsDBTreeView.Selection.CountSelectedRows()>0);
        ButtonEnable(createButton, true);
        ButtonEnable(readButton, enable);
        ButtonEnable(updateButton, enable);
        ButtonEnable(deleteButton, enable);
    }

    /// <summary>
    /// Gets the key of the selected listview row.
    /// </summary>
    /// <returns>The selected key.</returns>
    private string GetSelectedKey()
    {
        return ((string)_contactsDBStore.GetValue(_selectedContact, 4));
    }

    /// <summary>
    /// Adds a contact to internal dictionary and listview window row.
    /// </summary>
    /// <param name="contact">Contact.</param>
    private void AddContact(ContactRecord contact)
    {
        _contacts[contact.Id] = contact;
        _contactsDBStore.AppendValues(contact.LastName, contact.FirstName, 
                 contact.EmailAddress, contact.PhoneNumber, contact.Id);
    }

    /// <summary>
    /// Removes the selected contact.
    /// </summary>
    private void RemoveSelectedContact()
    {
        _contacts.Remove(GetSelectedKey());
        _contactsDBStore.Remove(ref _selectedContact);
    }

    /// <summary>
    /// Writes the contact records.
    /// </summary>
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

    /// <summary>
    /// Loads the contact records from CSV file.
    /// </summary>
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

    /// <summary>
    /// Adds a column to contact list view row.
    /// </summary>
    /// <param name="title">Title.</param>
    /// <param name="cellNo">Cell no.</param>
    private void AddListViewColumn(string title, int cellNo)
    {
        TreeViewColumn column = new TreeViewColumn();
        column.Title = title;
        CellRendererText cell = new CellRendererText();
        column.PackStart(cell, true);
        contactsDBTreeView.AppendColumn(column);
        column.AddAttribute(cell, "text", cellNo);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:MainWindow"/> class.
    /// </summary>
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

    /// <summary>
    /// On window delete event.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="a">The alpha component.</param>
    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    /// <summary>
    /// On the create button clicked.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
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

    /// <summary>
    /// On the read button clicked.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnReadButtonClicked(object sender, EventArgs e)
    {
        ContactDBDialog dialog = new ContactDBDialog(_contacts[GetSelectedKey()], false);
        dialog.Run();
        SetButtonsStatus();
    }

    /// <summary>
    /// On the update button clicked.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
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

    /// <summary>
    /// On the delete button clicked.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnDeleteButtonClicked(object sender, EventArgs e)
    {

        RemoveSelectedContact();
        WriteContactRecords();
        if (contactsDBTreeView.Selection.CountSelectedRows() > 0) {
            contactsDBTreeView.Selection.SelectIter(_selectedContact);
        }
        SetButtonsStatus();

    }

    /// <summary>
    /// On the contact list view cursor changed.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnContactsDBTreeViewCursorChanged(object sender, EventArgs e)
    {
        contactsDBTreeView.Selection.GetSelected(out _selectedContact);
        SetButtonsStatus();
    }

}
