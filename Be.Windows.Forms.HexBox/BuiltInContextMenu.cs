﻿using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Defines a build-in ContextMenuStrip manager for HexBox control to show Copy, Cut, Paste menu in contextmenu of the control.
    /// </summary>
    [TypeConverterAttribute(typeof(ExpandableObjectConverter))]
    public sealed class BuiltInContextMenu : Component
    {
        /// <summary>
        /// Contains the HexBox control.
        /// </summary>
        HexBox _hexBox;
        /// <summary>
        /// Contains the ContextMenuStrip control.
        /// </summary>
        ContextMenuStrip _contextMenuStrip;
        /// <summary>
        /// Contains the "Cut"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _cutToolStripMenuItem;
        /// <summary>
        /// Contains the "Copy"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _copyToolStripMenuItem;
        /// <summary>
        /// Contains the "CopyHex"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _copyHexToolStripMenuItem;
        /// <summary>
        /// Contains the "Paste"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _pasteToolStripMenuItem;
        /// <summary>
        /// Contains the "Paste"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _pasteHexToolStripMenuItem;
        /// <summary>
        /// Contains the "Select All"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _selectAllToolStripMenuItem;

        /// <summary>
        /// Initializes a new instance of BuildInContextMenu class.
        /// </summary>
        /// <param name="hexBox">the HexBox control</param>
        internal BuiltInContextMenu(HexBox hexBox)
        {
            _hexBox = hexBox;
            _hexBox.ByteProviderChanged += new EventHandler(HexBox_ByteProviderChanged);
        }

        /// <summary>
        /// If ByteProvider
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void HexBox_ByteProviderChanged(object sender, EventArgs e) => CheckBuiltInContextMenu();

        /// <summary>
        /// Assigns the ContextMenuStrip control to the HexBox control.
        /// </summary>
        void CheckBuiltInContextMenu()
        {
            if (Util.DesignMode) return;

            if (this._contextMenuStrip == null)
            {
                ContextMenuStrip cms = new ContextMenuStrip();
                _cutToolStripMenuItem = new ToolStripMenuItem(CutMenuItemTextInternal, CutMenuItemImage, new EventHandler(CutMenuItem_Click));
                cms.Items.Add(_cutToolStripMenuItem);

                cms.Items.Add(new ToolStripSeparator());

                _copyToolStripMenuItem = new ToolStripMenuItem(CopyMenuItemTextInternal, CopyMenuItemImage, new EventHandler(CopyMenuItem_Click));
                cms.Items.Add(_copyToolStripMenuItem);

                _copyHexToolStripMenuItem = new ToolStripMenuItem(CopyHexMenuItemTextInternal, CopyMenuItemImage, new EventHandler(CopyHexMenuItem_Click));
                cms.Items.Add(_copyHexToolStripMenuItem);

                cms.Items.Add(new ToolStripSeparator());

                _pasteToolStripMenuItem = new ToolStripMenuItem(PasteMenuItemTextInternal, PasteMenuItemImage, new EventHandler(PasteMenuItem_Click));
                cms.Items.Add(_pasteToolStripMenuItem);

                _pasteHexToolStripMenuItem = new ToolStripMenuItem(PasteHexMenuItemTextInternal, PasteMenuItemImage, new EventHandler(PasteHexMenuItem_Click));
                cms.Items.Add(_pasteHexToolStripMenuItem);

                cms.Items.Add(new ToolStripSeparator());

                _selectAllToolStripMenuItem = new ToolStripMenuItem(SelectAllMenuItemTextInternal, SelectAllMenuItemImage, new EventHandler(SelectAllMenuItem_Click));
                cms.Items.Add(_selectAllToolStripMenuItem);
                cms.Opening += new CancelEventHandler(BuildInContextMenuStrip_Opening);

                _contextMenuStrip = cms;
            }

            if (this._hexBox.ByteProvider == null && this._hexBox.ContextMenuStrip == this._contextMenuStrip)
                this._hexBox.ContextMenuStrip = null;
            else if (this._hexBox.ByteProvider != null && this._hexBox.ContextMenuStrip == null)
                this._hexBox.ContextMenuStrip = _contextMenuStrip;
        }

        /// <summary>
        /// Before opening the ContextMenuStrip, we manage the availability of the items.
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void BuildInContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            _cutToolStripMenuItem.Enabled = this._hexBox.CanCut();
            _copyToolStripMenuItem.Enabled = this._hexBox.CanCopy();
            _pasteToolStripMenuItem.Enabled = this._hexBox.CanPaste();
            _selectAllToolStripMenuItem.Enabled = this._hexBox.CanSelectAll();
        }

        /// <summary>
        /// The handler for the "Cut"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void CutMenuItem_Click(object sender, EventArgs e) => this._hexBox.Cut();

        /// <summary>
        /// The handler for the "Copy"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void CopyMenuItem_Click(object sender, EventArgs e) => this._hexBox.Copy();

        /// <summary>
        /// The handler for the "CopyHex"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void CopyHexMenuItem_Click(object sender, EventArgs e) => this._hexBox.Copy(true);

        /// <summary>
        /// The handler for the "Paste"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void PasteMenuItem_Click(object sender, EventArgs e) => this._hexBox.Paste();

        /// <summary>
        /// The handler for the "PasteHex"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void PasteHexMenuItem_Click(object sender, EventArgs e) => this._hexBox.Paste(true);

        /// <summary>
        /// The handler for the "Select All"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void SelectAllMenuItem_Click(object sender, EventArgs e) => this._hexBox.SelectAll();

        /// <summary>
        /// Gets or sets the custom text of the "Copy" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string CopyMenuItemText
        {
            get => _copyMenuItemText;
            set => _copyMenuItemText = value;
        }
        string _copyMenuItemText;

        /// <summary>
        /// Gets or sets the custom text of the "CopyHex" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string CopyHexMenuItemText
        {
            get => _copyHexMenuItemText;
            set => _copyHexMenuItemText = value;
        }
        string _copyHexMenuItemText;

        /// <summary>
        /// Gets or sets the custom text of the "Cut" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string CutMenuItemText
        {
            get => _cutMenuItemText;
            set => _cutMenuItemText = value;
        }
        string _cutMenuItemText;

        /// <summary>
        /// Gets or sets the custom text of the "Paste" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string PasteMenuItemText
        {
            get => _pasteMenuItemText;
            set => _pasteMenuItemText = value;
        }
        string _pasteMenuItemText;

        /// <summary>
        /// Gets or sets the custom text of the "PasteHex" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string PasteHexMenuItemText
        {
            get => _pasteHexMenuItemText;
            set => _pasteHexMenuItemText = value;
        }
        string _pasteHexMenuItemText;

        /// <summary>
        /// Gets or sets the custom text of the "Select All" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string SelectAllMenuItemText
        {
            get => _selectAllMenuItemText;
            set => _selectAllMenuItemText = value;
        }
        string _selectAllMenuItemText = null;

        /// <summary>
        /// Gets the text of the "Cut" ContextMenuStrip item.
        /// </summary>
        internal string CutMenuItemTextInternal => !string.IsNullOrEmpty(CutMenuItemText) ? CutMenuItemText : "Cut";
        /// <summary>
        /// Gets the text of the "Copy" ContextMenuStrip item.
        /// </summary>
        internal string CopyMenuItemTextInternal => !string.IsNullOrEmpty(CopyMenuItemText) ? CopyMenuItemText : "Copy";
        /// <summary>
        /// Gets the text of the "CopyHex" ContextMenuStrip item.
        /// </summary>
        internal string CopyHexMenuItemTextInternal => !string.IsNullOrEmpty(CopyHexMenuItemText) ? CopyHexMenuItemText : "Copy Hex";
        /// <summary>
        /// Gets the text of the "Paste" ContextMenuStrip item.
        /// </summary>
        internal string PasteMenuItemTextInternal => !string.IsNullOrEmpty(PasteMenuItemText) ? PasteMenuItemText : "Paste";
        /// <summary>
        /// Gets the text of the "Paste" ContextMenuStrip item.
        /// </summary>
        internal string PasteHexMenuItemTextInternal => !string.IsNullOrEmpty(PasteHexMenuItemText) ? PasteHexMenuItemText : "Paste Hex";
        /// <summary>
        /// Gets the text of the "Select All" ContextMenuStrip item.
        /// </summary>
        internal string SelectAllMenuItemTextInternal => !string.IsNullOrEmpty(SelectAllMenuItemText) ? SelectAllMenuItemText : "SelectAll";

        /// <summary>
        /// Gets or sets the image of the "Cut" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image CutMenuItemImage
        {
            get => _cutMenuItemImage;
            set => _cutMenuItemImage = value;
        }
        Image _cutMenuItemImage = null;

        /// <summary>
        /// Gets or sets the image of the "Copy" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image CopyMenuItemImage
        {
            get => _copyMenuItemImage;
            set => _copyMenuItemImage = value;
        }
        Image _copyMenuItemImage = null;

        /// <summary>
        /// Gets or sets the image of the "Paste" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image PasteMenuItemImage
        {
            get => _pasteMenuItemImage;
            set => _pasteMenuItemImage = value;
        }
        Image _pasteMenuItemImage = null;

        /// <summary>
        /// Gets or sets the image of the "Select All" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image SelectAllMenuItemImage
        {
            get => _selectAllMenuItemImage;
            set => _selectAllMenuItemImage = value;
        }
        Image _selectAllMenuItemImage = null;

        /// <summary>
        /// Contains the ContextMenuStrip control.
        /// </summary>
        /// <returns>ContextMenuStrip</returns>
        public ContextMenuStrip GetContextMenuStrip()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _contextMenuStrip;
        }
        /// <summary>
        /// Contains the "Cut"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetCutToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _cutToolStripMenuItem;
        }
        /// <summary>
        /// Contains the "Copy"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetCopyToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _copyToolStripMenuItem;
        }
        /// <summary>
        /// Contains the "CopyHex"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetCopyHexToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _copyHexToolStripMenuItem;
        }
        /// <summary>
        /// Contains the "Paste"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetPasteToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _pasteToolStripMenuItem;
        }
        /// <summary>
        /// Contains the "Paste"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetPasteHexToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _pasteHexToolStripMenuItem;
        }
        /// <summary>
        /// Contains the "Select All"-ToolStripMenuItem object.
        /// </summary>
        public ToolStripMenuItem GetSelectAllToolStripMenuItem()
        {
            if (_contextMenuStrip == null) CheckBuiltInContextMenu();
            return _selectAllToolStripMenuItem;
        }
    }
}
