//
// Author: Robert Tizzard
//
// Program: Contacts Database.
//
// Description: C#/Gtk# based contact database main window class.
//
// Copyright 2019.
//
using System;
using System.Configuration;
using System.Collections.Generic;
using ContactsDB;
using Gtk;

/// <summary>
/// Main window class for contacts database program.
/// </summary>
public partial class MainWindow : Gtk.Window
{
    private ContactDBStore _contactDBStore;                 // Database store class (CSV/SQlite)
    private ListStore _contactsListViewStore;               // list view store for main window
    private TreeIter _selectedContact;                      // Currently selected list view contact
    private Dictionary<string, ContactRecord> _contacts;    // Dictionary of contact records

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
    /// Sets status of create/read/update/delete buttons.
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
    /// Gets the key of the currently selected list view row.
    /// </summary>
    /// <returns>The selected key.</returns>
    private string GetSelectedKey()
    {
        return ((string)_contactsListViewStore.GetValue(_selectedContact, 4));
    }

    /// <summary>
    /// Adds a contact to list view.
    /// </summary>
    /// <param name="contact">Contact.</param>
    private void AddContactToListVew(ContactRecord contact)
    {
        _contactsListViewStore.AppendValues(contact.LastName, contact.FirstName, 
                 contact.Email, contact.PhoneNo, contact.Id);
    }

    /// <summary>
    /// Removes the selected contact from list view.
    /// </summary>
    private void RemoveSelectedContactFromListView()
    {
        _contactsListViewStore.Remove(ref _selectedContact);
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
    /// Sets the configuration.
    /// </summary>
    private void SetConfiguration()
    {
        // Only supports CSV and SQLITE database at the moment.

        if (ConfigurationManager.AppSettings["StoreType"] == "CSV")
        {
            _contactDBStore = new ContactCSV(ConfigurationManager.AppSettings["StoreFile"]);
        }
        else if (ConfigurationManager.AppSettings["StoreType"] == "SQLITE")
        {
            _contactDBStore = new ContactSQLite(ConfigurationManager.AppSettings["StoreFile"]);
        }
        else
        {
            throw new Exception("Invalid Store type; Can only be CSV or SQLITE.");
        }
        Title += " (" + ConfigurationManager.AppSettings["StoreType"] + ")";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:MainWindow"/> class.
    /// </summary>
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        // Set configuration

        SetConfiguration();

        // Add list voew columns

        AddListViewColumn("Last Name", 0);
        AddListViewColumn("First Name", 1);
        AddListViewColumn("E-Mail", 2);
        AddListViewColumn("Phone No.", 3);
        AddListViewColumn("Id", 4);

        // Create list view and store

        _contactsListViewStore = new ListStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string));
        contactsDBTreeView.Model = _contactsListViewStore;
        _contactsListViewStore.SetSortColumnId(0, SortType.Ascending);

        // Load contacts and display

        _contacts = _contactDBStore.LoadContactRecords();

        foreach(var id in _contacts.Keys)
        {
            AddContactToListVew(_contacts[id]);
        }

        // Select first contacts record

        if (_contacts.Count > 0)
        {
            if (_contactsListViewStore.GetIterFirst(out _selectedContact))
            {
                contactsDBTreeView.Selection.SelectIter(_selectedContact);
            }

        }

        // Set button(s) status

        SetButtonsStatus();

    }

    /// <summary>
    /// On window delete event (flush contacts records to store if necessary).
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="a">The alpha component.</param>
    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        _contactDBStore.FlushContactRecords(_contacts);
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
        // Create contacts record and display details dialog to be filled in

        ContactRecord contact = new ContactRecord();
        contact.Id = (_contactDBStore.NextID + 1).ToString();
        ContactDBDialog dialog = new ContactDBDialog(contact, true);

        if ((ResponseType)dialog.Run() == ResponseType.Ok)
        {
            // Add new contact record to list view and permanent store

            _contactDBStore.NextID++;
            _contacts[contact.Id] = contact;
            AddContactToListVew(contact);
            _contactDBStore.WriteContactRecord(contact);

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
        // Display contact details for seelcted record
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
        // Get currently selected contact record and display update detaisl dialog

        ContactRecord contact = _contacts[GetSelectedKey()];
        ContactDBDialog dialog = new ContactDBDialog(contact, true);

        if ((ResponseType)dialog.Run() == ResponseType.Ok)
        {
            // Update list view and data store to reflect any changes
            _contactsListViewStore.SetValues(_selectedContact, contact.LastName, contact.FirstName,
                                            contact.Email, contact.PhoneNo);
            _contactDBStore.UpdateContactRecord(contact);
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

        // Get currently selected record key and delete details from store and list view

        var key = GetSelectedKey();

        _contactDBStore.DeleteContactRecord(_contacts[key]);
        _contacts.Remove(key);

        RemoveSelectedContactFromListView();

        // Reselect to record inlist view 

        if (contactsDBTreeView.Selection.CountSelectedRows() > 0) {
            contactsDBTreeView.Selection.SelectIter(_selectedContact);
        }

        SetButtonsStatus();

    }

    /// <summary>
    /// On the contact list view cursor changed store new selected contact list view row
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnContactsDBTreeViewCursorChanged(object sender, EventArgs e)
    {
        contactsDBTreeView.Selection.GetSelected(out _selectedContact);
        SetButtonsStatus();
    }

}
