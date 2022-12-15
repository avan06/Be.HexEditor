using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace Be.HexEditor
{
    public partial class FormHexEditor : Core.FormEx
    {
        FormFind _formFind;
        FindOptions _findOptions = new FindOptions();
        FormGoTo _formGoto = new FormGoTo();
        string _fileName;

        public FormHexEditor()
        {
            InitializeComponent();

            Init();

            hexBox.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.Size, SystemFonts.MessageBoxFont.Style);

            toolStrip.Renderer.RenderToolStripBorder += new ToolStripRenderEventHandler(Renderer_RenderToolStripBorder);
        }

        /// <summary>
        /// Removes the border on the right of the tool strip
        /// </summary>
        /// <param name="sender">the renderer</param>
        /// <param name="e">the event args</param>
        void Renderer_RenderToolStripBorder(object sender, ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip.GetType() != typeof(ToolStrip)) return;

            e.Graphics.DrawLine(new Pen(new SolidBrush(SystemColors.Control)), new Point(toolStrip.Width - 1, 0),
                new Point(toolStrip.Width - 1, toolStrip.Height));
        }

        /// <summary>
        /// Initializes the hex editor�s main form
        /// </summary>
        void Init()
        {
            DisplayText();

            ManageAbility();

            UpdateBitControlVisibility();

            //var selected = ;
            var defConverter = new DefaultByteCharConverter();
            ToolStripMenuItem miDefault = new ToolStripMenuItem();
            miDefault.Text = defConverter.ToString();
            miDefault.Tag = defConverter;
            miDefault.Click += new EventHandler(EncodingMenuItem_Clicked);

            encodingToolStripComboBox.Items.Add(defConverter);
            encodingToolStripMenuItem.DropDownItems.Add(miDefault);
            encodingToolStripComboBox.SelectedIndex = 0;

            //500  : IBM EBCDIC;
            //932  : Japanese (Shift-JIS);
            //949  : Korean (Unified Hangul Code);
            //950  : Chinese Traditional (Big5);
            //936  : Chinese Simplified (GB2312);
            //65001: Unicode (UTF-8)
            //1200 : Unicode UTF-16, little endian byte order;
            //1201 : Unicode UTF-16, big endian byte order;
            int[] codepages = { 500, 932, 949, 950, 936, 65001, 1200, 1201 };
            foreach (int codepage in codepages)
            {
                try
                {
                    var encodeConverter = new EncodingByteCharProvider(codepage);
                    ToolStripMenuItem miEbcdic = new ToolStripMenuItem();
                    miEbcdic.Text = encodeConverter.ToString();
                    miEbcdic.Tag = encodeConverter;
                    miEbcdic.Click += new EventHandler(EncodingMenuItem_Clicked);

                    encodingToolStripComboBox.Items.Add(encodeConverter);
                    encodingToolStripMenuItem.DropDownItems.Add(miEbcdic);
                }
                catch { }
            }

            HexBox.ByteGroupingType[] byteGroupingTypes = (HexBox.ByteGroupingType[])Enum.GetValues(typeof(HexBox.ByteGroupingType));
            foreach (var byteGroupingType in byteGroupingTypes) ByteGroupToolStripComboBox.Items.Add(byteGroupingType);
            ByteGroupToolStripComboBox.SelectedIndex = 0;

            for (int idx = 0; idx <= 0x10; idx++) GroupSizeToolStripComboBox.Items.Add("GroupSize:" + idx);
            GroupSizeToolStripComboBox.SelectedIndex = 0;

            UpdateFormWidth();
        }

        void EncodingMenuItem_Clicked(object sender, EventArgs e) => encodingToolStripComboBox.SelectedItem = ((ToolStripMenuItem)sender).Tag;

        /// <summary>
        /// Updates the File size status label
        /// </summary>
        void UpdateFileSizeStatus()
        {
            if (hexBox.ByteProvider == null) fileSizeToolStripStatusLabel.Text = string.Empty;
            else fileSizeToolStripStatusLabel.Text = Util.GetDisplayBytes(hexBox.ByteProvider.Length);
        }

        /// <summary>
        /// Displays the file name in the Form�s text property
        /// </summary>
        /// <param name="fileName">the file name to display</param>
        void DisplayText()
        {
            if (_fileName != null && _fileName.Length > 0)
            {
                string textFormat = "{0}{1} - {2}";
                string readOnly = ((DynamicFileByteProvider)hexBox.ByteProvider).ReadOnly ? strings.Readonly : "";
                string text = Path.GetFileName(_fileName);
                Text = string.Format(textFormat, text, readOnly, Program.SoftwareName);
            }
            else Text = Program.SoftwareName;
        }

        /// <summary>
        /// Manages enabling or disabling of menu items and toolstrip buttons.
        /// </summary>
        void ManageAbility()
        {
            if (hexBox.ByteProvider == null)
            {
                saveToolStripMenuItem.Enabled = saveToolStripButton.Enabled = false;

                findToolStripMenuItem.Enabled = false;
                findNextToolStripMenuItem.Enabled = false;
                goToToolStripMenuItem.Enabled = false;

                selectAllToolStripMenuItem.Enabled = false;
            }
            else
            {
                saveToolStripMenuItem.Enabled = saveToolStripButton.Enabled = hexBox.ByteProvider.HasChanges();

                findToolStripMenuItem.Enabled = true;
                findNextToolStripMenuItem.Enabled = true;
                goToToolStripMenuItem.Enabled = true;

                selectAllToolStripMenuItem.Enabled = true;
            }

            ManageAbilityForCopyAndPaste();
        }

        /// <summary>
        /// Manages enabling or disabling of menustrip items and toolstrip buttons for copy and paste
        /// </summary>
        void ManageAbilityForCopyAndPaste()
        {
            copyHexToolStripMenuItem.Enabled = copyToolStripSplitButton.Enabled = copyToolStripMenuItem.Enabled = hexBox.CanCopy();

            cutToolStripButton.Enabled = cutToolStripMenuItem.Enabled = hexBox.CanCut();
            pasteHexToolStripMenuItem.Enabled = pasteToolStripSplitButton.Enabled = pasteToolStripMenuItem.Enabled = hexBox.CanPaste();
        }

        /// <summary>
        /// Shows the open file dialog.
        /// </summary>
        void OpenFile()
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK) OpenFile(openFileDialog.FileName);
        }

        /// <summary>
        /// Opens a file.
        /// </summary>
        /// <param name="fileName">the file name of the file to open</param>
        public void OpenFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Program.ShowMessage(strings.FileDoesNotExist);
                return;
            }

            if (CloseFile() == DialogResult.Cancel)
                return;

            try
            {
                DynamicFileByteProvider dynamicFileByteProvider;
                try
                {
                    // try to open in write mode
                    dynamicFileByteProvider = new DynamicFileByteProvider(fileName);
                    dynamicFileByteProvider.Changed += new EventHandler(byteProvider_Changed);
                    dynamicFileByteProvider.LengthChanged += new EventHandler(byteProvider_LengthChanged);
                }
                catch (IOException) // write mode failed
                {
                    try
                    {
                        // try to open in read-only mode
                        dynamicFileByteProvider = new DynamicFileByteProvider(fileName, true);
                        if (Program.ShowQuestion(strings.OpenReadonly) == DialogResult.No)
                        {
                            dynamicFileByteProvider.Dispose();
                            return;
                        }
                    }
                    catch (IOException) // read-only also failed
                    {
                        // file cannot be opened
                        Program.ShowError(strings.OpenFailed);
                        return;
                    }
                }

                hexBox.ByteProvider = dynamicFileByteProvider;
                _fileName = fileName;

                DisplayText();

                UpdateFileSizeStatus();

                RecentFileHandler.AddFile(fileName);
            }
            catch (Exception ex1)
            {
                Program.ShowError(ex1);
                return;
            }
            finally
            {

                ManageAbility();
            }
        }

        /// <summary>
        /// Saves the current file.
        /// </summary>
        void SaveFile()
        {
            if (hexBox.ByteProvider == null) return;

            try
            {
                DynamicFileByteProvider dynamicFileByteProvider = hexBox.ByteProvider as DynamicFileByteProvider;
                dynamicFileByteProvider.ApplyChanges();
            }
            catch (Exception ex1)
            {
                Program.ShowError(ex1);
            }
            finally
            {
                ManageAbility();
            }
        }

        /// <summary>
        /// Closes the current file
        /// </summary>
        /// <returns>OK, if the current file was closed.</returns>
        DialogResult CloseFile()
        {
            if (hexBox.ByteProvider == null) return DialogResult.OK;

            try

            {
                if (hexBox.ByteProvider != null && hexBox.ByteProvider.HasChanges())
                {
                    DialogResult res = MessageBox.Show(strings.SaveChangesQuestion,
                        Program.SoftwareName,
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (res == DialogResult.Yes)
                    {
                        SaveFile();
                        CleanUp();
                    }
                    else if (res == DialogResult.No) CleanUp();
                    else if (res == DialogResult.Cancel) return res;

                    return res;
                }
                else
                {
                    CleanUp();
                    return DialogResult.OK;
                }
            }
            finally
            {
                ManageAbility();
            }
        }

        void CleanUp()
        {
            if (hexBox.ByteProvider != null)
            {
                IDisposable byteProvider = hexBox.ByteProvider as IDisposable;
                if (byteProvider != null) byteProvider.Dispose();

                hexBox.ByteProvider = null;
            }
            _fileName = null;
            DisplayText();
        }

        /// <summary>
        /// Opens the Find dialog
        /// </summary>
        void Find()
        {
            ShowFind();
        }

        /// <summary>
        /// Creates a new FormFind dialog
        /// </summary>
        /// <returns>the form find dialog</returns>
        FormFind ShowFind()
        {
            if (_formFind == null || _formFind.IsDisposed)
            {
                _formFind = new FormFind();
                _formFind.HexBox = hexBox;
                _formFind.FindOptions = _findOptions;
                _formFind.Show(this);
            }
            else
            {
                _formFind.Focus();
            }
            return _formFind;
        }

        /// <summary>
        /// Find next match
        /// </summary>
        void FindNext()
        {
            ShowFind().FindNext();
        }

        /// <summary>
        /// Aborts the current find process
        /// </summary>
        void FormFindCancel_Closed(object sender, EventArgs e)
        {
            hexBox.AbortFind();
        }

        /// <summary>
        /// Displays the goto byte dialog.
        /// </summary>
        void Goto()
        {
            _formGoto.SetMaxByteIndex(hexBox.ByteProvider.Length);
            _formGoto.SetDefaultValue(hexBox.SelectionStart);
            if (_formGoto.ShowDialog() == DialogResult.OK)
            {
                hexBox.SelectionStart = _formGoto.GetByteIndex();
                hexBox.SelectionLength = 1;
                hexBox.Focus();
            }
        }

        /// <summary>
        /// Enables drag&drop
        /// </summary>
        void hexBox_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }

        /// <summary>
        /// Processes a file drop
        /// </summary>
        void hexBox_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            object oFileNames = e.Data.GetData(DataFormats.FileDrop);
            string[] fileNames = (string[])oFileNames;
            if (fileNames.Length == 1) OpenFile(fileNames[0]);
        }

        void hexBox_Copied(object sender, EventArgs e)
        {
            ManageAbilityForCopyAndPaste();
        }

        void hexBox_SelectionLengthChanged(object sender, System.EventArgs e)
        {
            ManageAbilityForCopyAndPaste();
        }

        void hexBox_SelectionStartChanged(object sender, System.EventArgs e)
        {
            ManageAbilityForCopyAndPaste();
        }

        void Position_Changed(object sender, EventArgs e)
        {
            toolStripStatusLabel.Text = string.Format("Ln {0}    Col {1}",
                hexBox.CurrentLine, hexBox.CurrentPositionInLine);

            string bitPresentation = string.Empty;

            byte? currentByte = hexBox.ByteProvider != null && hexBox.ByteProvider.Length > hexBox.SelectionStart
                ? hexBox.ByteProvider.ReadByte(hexBox.SelectionStart)
                : (byte?)null;

            BitInfo bitInfo = currentByte != null ? new BitInfo((byte)currentByte, hexBox.SelectionStart) : null;

            if (bitInfo != null)
            {
                byte currentByteNotNull = (byte)currentByte;
                bitPresentation = string.Format("Bits of Byte {0}: {1}"
                    , hexBox.SelectionStart
                    , bitInfo.ToString()
                    );
            }

            bitToolStripStatusLabel.Text = bitPresentation;

            bitControl1.BitInfo = bitInfo;
        }

        void byteProvider_Changed(object sender, EventArgs e)
        {
            ManageAbility();
        }

        void byteProvider_LengthChanged(object sender, EventArgs e)
        {
            UpdateFileSizeStatus();
        }

        void open_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        void save_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void close_Click(object sender, EventArgs e)
        {
            if (hexBox.ByteProvider == null) return;

            try
            {
                DynamicFileByteProvider dynamicFileByteProvider = hexBox.ByteProvider as DynamicFileByteProvider;
                dynamicFileByteProvider.Changed -= new EventHandler(byteProvider_Changed);
                dynamicFileByteProvider.LengthChanged -= new EventHandler(byteProvider_LengthChanged);
                dynamicFileByteProvider.Dispose();
                dynamicFileByteProvider = null;
                hexBox.ByteProvider = null;
                _fileName = null;
                DisplayText();
                UpdateFileSizeStatus();
            }
            catch (Exception ex1)
            {
                Program.ShowError(ex1);
            }
            finally
            {
                ManageAbility();
            }

        }

        void cut_Click(object sender, EventArgs e)
        {
            hexBox.Cut();
        }

        private void copy_Click(object sender, EventArgs e)
        {
            hexBox.Copy();
        }

        private void copyHex_Click(object sender, EventArgs e)
        {
            hexBox.Copy(true);
        }

        void paste_Click(object sender, EventArgs e)
        {
            hexBox.Paste();
        }

        void pasteHex_Click(object sender, EventArgs e)
        {
            hexBox.Paste(true);
        }

        void find_Click(object sender, EventArgs e)
        {
            Find();
        }

        void findNext_Click(object sender, EventArgs e)
        {
            FindNext();
        }

        void goTo_Click(object sender, EventArgs e)
        {
            Goto();
        }

        void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hexBox.SelectAll();
        }

        void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        void about_Click(object sender, EventArgs e)
        {
            new FormAbout().ShowDialog();
        }

        void recentFiles_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            RecentFileHandler.FileMenuItem fmi = (RecentFileHandler.FileMenuItem)e.ClickedItem;
            OpenFile(fmi.FileName);
        }

        void options_Click(object sender, EventArgs e)
        {
            new FormOptions().ShowDialog();
        }

        void FormHexEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            var result = CloseFile();
            if (result == DialogResult.Cancel) e.Cancel = true;
        }

        void toolStripEncoding_SelectedIndexChanged(object sender, EventArgs e)
        {
            hexBox.ByteCharConverter = encodingToolStripComboBox.SelectedItem as IByteCharConverter;

            foreach (ToolStripMenuItem encodingMenuItem in encodingToolStripMenuItem.DropDownItems)
                encodingMenuItem.Checked = (encodingMenuItem.Tag == hexBox.ByteCharConverter);
        }

        void bitsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            UpdateBitControlVisibility();
        }

        void UpdateBitControlVisibility()
        {
            if (Util.DesignMode) return;
            //if (this.bitControl1.Visible == bitsToolStripMenuItem.Checked)
            //{
            //    return;
            //}
            if (bitsToolStripMenuItem.Checked)
            {
                hexBox.Height -= bitControl1.Height;
                bitControl1.Visible = true;
            }
            else
            {
                hexBox.Height += bitControl1.Height;
                bitControl1.Visible = false;
            }
        }

        void bitControl1_BitChanged(object sender, EventArgs e)
        {
            hexBox.ByteProvider.WriteByte(bitControl1.BitInfo.Position, bitControl1.BitInfo.Value);
            hexBox.Invalidate();
        }

        void menuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        void hexBox_RequiredWidthChanged(object sender, EventArgs e)
        {
            UpdateFormWidth();
        }

        void UpdateFormWidth()
        {
            Width = hexBox.RequiredWidth + 70;
        }

        private void ByteGroupToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var byteGroupComboBox = (ToolStripComboBox)sender;
            HexBox.ByteGroupingType byteGroupingType = (HexBox.ByteGroupingType)byteGroupComboBox.SelectedItem;
            hexBox.ByteGrouping = byteGroupingType;
        }

        private void GroupSizeToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var groupSizeComboBox = (ToolStripComboBox)sender;
            int groupSize = groupSizeComboBox.SelectedIndex;

            hexBox.GroupSeparatorVisible = groupSize > 0;
            hexBox.GroupSize = groupSize;
        }
    }
}