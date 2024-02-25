using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Security.Permissions;
using System.Windows.Forms.VisualStyles;
using System.Collections.Generic;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Represents a hex box control.
    /// </summary>
    [ToolboxBitmap(typeof(HexBox), "HexBox.bmp")]
    public class HexBox : Control
    {
        #region IKeyInterpreter interface
        /// <summary>
        /// Defines a user input handler such as for mouse and keyboard input
        /// </summary>
        interface IKeyInterpreter
        {
            /// <summary>
            /// Activates mouse events
            /// </summary>
            void Activate();
            /// <summary>
            /// Deactivate mouse events
            /// </summary>
            void Deactivate();
            /// <summary>
            /// Preprocesses WM_KEYUP window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmKeyUp(ref Message m);
            /// <summary>
            /// Preprocesses WM_CHAR window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmChar(ref Message m);
            /// <summary>
            /// Preprocesses WM_KEYDOWN window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmKeyDown(ref Message m);
            /// <summary>
            /// Gives some information about where to place the caret.
            /// </summary>
            /// <param name="byteIndex">the index of the byte</param>
            /// <returns>the position where the caret is to place.</returns>
            PointF GetCaretPointF(long byteIndex);
        }
        #endregion

        #region EmptyKeyInterpreter class
        /// <summary>
        /// Represents an empty input handler without any functionality. 
        /// If is set ByteProvider to null, then this interpreter is used.
        /// </summary>
        class EmptyKeyInterpreter : IKeyInterpreter
        {
            HexBox _hexBox;

            public EmptyKeyInterpreter(HexBox hexBox) => _hexBox = hexBox;

            #region IKeyInterpreter Members
            public void Activate() { }
            public void Deactivate() { }

            public bool PreProcessWmKeyUp(ref Message m) => _hexBox.BasePreProcessMessage(ref m);

            public bool PreProcessWmChar(ref Message m) => _hexBox.BasePreProcessMessage(ref m);

            public bool PreProcessWmKeyDown(ref Message m) => _hexBox.BasePreProcessMessage(ref m);

            public PointF GetCaretPointF(long byteIndex) => new PointF();
            #endregion
        }
        #endregion

        #region KeyInterpreter class
        /// <summary>
        /// Handles user input such as mouse and keyboard input during hex view edit
        /// </summary>
        class KeyInterpreter : IKeyInterpreter
        {
            /// <summary>
            /// Delegate for key-down processing.
            /// </summary>
            /// <param name="m">the message object contains key data information</param>
            /// <returns>True, if the message was processed</returns>
            delegate bool MessageDelegate(ref Message m);

            #region Fields
            /// <summary>
            /// Contains the parent HexBox control
            /// </summary>
            protected HexBox _hexBox;

            /// <summary>
            /// Contains True, if shift key is down
            /// </summary>
            protected bool _shiftDown;
            /// <summary>
            /// Contains True, if mouse is down
            /// </summary>
            bool _mouseDown;
            /// <summary>
            /// Contains the selection start position info
            /// </summary>
            BytePositionInfo _bpiStart;
            /// <summary>
            /// Contains the current mouse selection position info
            /// </summary>
            BytePositionInfo _bpi;
            /// <summary>
            /// Contains all message handlers of key interpreter key down message
            /// </summary>
            Dictionary<Keys, MessageDelegate> _messageHandlers;
            #endregion

            #region Ctors
            public KeyInterpreter(HexBox hexBox) => _hexBox = hexBox;
            #endregion

            #region Activate, Deactive methods
            public virtual void Activate()
            {
                _hexBox.MouseDown += new MouseEventHandler(BeginMouseSelection);
                _hexBox.MouseMove += new MouseEventHandler(UpdateMouseSelection);
                _hexBox.MouseUp += new MouseEventHandler(EndMouseSelection);
            }

            public virtual void Deactivate()
            {
                _hexBox.MouseDown -= new MouseEventHandler(BeginMouseSelection);
                _hexBox.MouseMove -= new MouseEventHandler(UpdateMouseSelection);
                _hexBox.MouseUp -= new MouseEventHandler(EndMouseSelection);
            }
            #endregion

            #region Mouse selection methods
            void BeginMouseSelection(object sender, MouseEventArgs e)
            {
                System.Diagnostics.Debug.WriteLine("BeginMouseSelection()", "KeyInterpreter");

                if (e.Button != MouseButtons.Left) return;

                _mouseDown = true;

                if (!_shiftDown)
                {
                    _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    UpdateMouseSelection(this, e);
                }
            }

            void UpdateMouseSelection(object sender, MouseEventArgs e)
            {
                if (!_mouseDown) return;

                _bpi = GetBytePositionInfo(new Point(e.X, e.Y));
                long selEnd = _bpi.Index;
                long realselStart;
                long realselLength;

                if (selEnd < _bpiStart.Index)
                {
                    realselStart = selEnd;
                    realselLength = _bpiStart.Index - selEnd;
                }
                else if (selEnd > _bpiStart.Index)
                {
                    realselStart = _bpiStart.Index;
                    realselLength = selEnd - realselStart;
                }
                else
                {
                    realselStart = _hexBox._bytePos;
                    realselLength = 0;
                }

                if (realselStart != _hexBox._bytePos || realselLength != _hexBox._selectionLength)
                {
                    _hexBox.InternalSelect(realselStart, realselLength);
                    _hexBox.ScrollByteIntoView(_bpi.Index);
                }
            }

            void EndMouseSelection(object sender, MouseEventArgs e) => _mouseDown = false;
            #endregion

            #region PrePrcessWmKeyDown methods
            public virtual bool PreProcessWmKeyDown(ref Message m)
            {
                System.Diagnostics.Debug.WriteLine("PreProcessWmKeyDown(ref Message m)", "KeyInterpreter");

                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                // detect whether key down event should be raised
                var hasMessageHandler = MessageHandlers.ContainsKey(keyData);
                if (hasMessageHandler && RaiseKeyDown(keyData)) return true;

                MessageDelegate messageHandler = hasMessageHandler ? MessageHandlers[keyData] : messageHandler = new MessageDelegate(PreProcessWmKeyDown_Default);

                return messageHandler(ref m);
            }

            protected bool PreProcessWmKeyDown_Default(ref Message m)
            {
                _hexBox.ScrollByteIntoView();
                return _hexBox.BasePreProcessMessage(ref m);
            }

            protected bool RaiseKeyDown(Keys keyData)
            {
                KeyEventArgs e = new KeyEventArgs(keyData);
                _hexBox.OnKeyDown(e);
                return e.Handled;
            }

            protected virtual bool PreProcessWmKeyDown_Left(ref Message m) => PerformPosMoveLeft();

            protected virtual bool PreProcessWmKeyDown_Up(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (!(pos == 0 && cPos == 0))
                {
                    pos = Math.Max(-1, pos - _hexBox._iHexMaxHBytes);
                    if (pos == -1) return true;

                    _hexBox.SetPosition(pos);

                    if (pos < _hexBox._startByte)
                    {
                        _hexBox.PerformScrollLineUp();
                    }

                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                }

                _hexBox.ScrollByteIntoView();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Right(ref Message m) => PerformPosMoveRight();

            protected virtual bool PreProcessWmKeyDown_Down(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length && cPos == 0) return true;

                pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxHBytes);

                if (pos == _hexBox._byteProvider.Length) cPos = 0;

                _hexBox.SetPosition(pos, cPos);

                if (pos > _hexBox._endByte - 1) _hexBox.PerformScrollLineDown();

                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_PageUp(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos == 0 && cPos == 0)
                    return true;

                pos = Math.Max(0, pos - _hexBox._iHexMaxBytes);
                if (pos == 0)
                    return true;

                _hexBox.SetPosition(pos);

                if (pos < _hexBox._startByte)
                {
                    _hexBox.PerformScrollPageUp();
                }

                _hexBox.ReleaseSelection();
                _hexBox.UpdateCaret();
                _hexBox.Invalidate();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_PageDown(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length && cPos == 0) return true;

                pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxBytes);

                if (pos == _hexBox._byteProvider.Length) cPos = 0;

                _hexBox.SetPosition(pos, cPos);

                if (pos > _hexBox._endByte - 1) _hexBox.PerformScrollPageDown();

                _hexBox.ReleaseSelection();
                _hexBox.UpdateCaret();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftLeft(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos + sel < 1)
                    return true;

                if (pos + sel <= _bpiStart.Index)
                {
                    if (pos == 0)
                        return true;

                    pos--;
                    sel++;
                }
                else
                {
                    sel = Math.Max(0, sel - 1);
                }

                _hexBox.ScrollByteIntoView();
                _hexBox.InternalSelect(pos, sel);

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftUp(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos - _hexBox._iHexMaxHBytes < 0 && pos <= _bpiStart.Index)
                    return true;

                if (_bpiStart.Index >= pos + sel)
                {
                    pos = pos - _hexBox._iHexMaxHBytes;
                    sel += _hexBox._iHexMaxHBytes;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }
                else
                {
                    sel -= _hexBox._iHexMaxHBytes;
                    if (sel < 0)
                    {
                        pos = _bpiStart.Index + sel;
                        sel = -sel;
                        _hexBox.InternalSelect(pos, sel);
                        _hexBox.ScrollByteIntoView();
                    }
                    else
                    {
                        sel -= _hexBox._iHexMaxHBytes;
                        _hexBox.InternalSelect(pos, sel);
                        _hexBox.ScrollByteIntoView(pos + sel);
                    }
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftRight(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos + sel >= _hexBox._byteProvider.Length) return true;

                if (_bpiStart.Index <= pos)
                {
                    sel++;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView(pos + sel);
                }
                else
                {
                    pos++;
                    sel = Math.Max(0, sel - 1);
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftDown(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                long max = _hexBox._byteProvider.Length;

                if (pos + sel + _hexBox._iHexMaxHBytes > max) return true;

                if (_bpiStart.Index <= pos)
                {
                    sel += _hexBox._iHexMaxHBytes;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView(pos + sel);
                }
                else
                {
                    sel -= _hexBox._iHexMaxHBytes;
                    if (sel < 0)
                    {
                        pos = _bpiStart.Index;
                        sel = -sel;
                    }
                    else
                    {
                        pos += _hexBox._iHexMaxHBytes;
                        //sel -= _hexBox._iHexMaxHBytes;
                    }

                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Tab(ref Message m)
            {
                if (_hexBox._stringViewVisible && _hexBox._keyInterpreter.GetType() == typeof(KeyInterpreter))
                {
                    _hexBox.ActivateStringKeyInterpreter();
                    _hexBox.ScrollByteIntoView();
                    _hexBox.ReleaseSelection();
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                    return true;
                }

                if (_hexBox.Parent == null) return true;
                _hexBox.Parent.SelectNextControl(_hexBox, true, true, true, true);
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftTab(ref Message m)
            {
                if (_hexBox._keyInterpreter is StringKeyInterpreter)
                {
                    _shiftDown = false;
                    _hexBox.ActivateKeyInterpreter();
                    _hexBox.ScrollByteIntoView();
                    _hexBox.ReleaseSelection();
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                    return true;
                }

                if (_hexBox.Parent == null) return true;
                _hexBox.Parent.SelectNextControl(_hexBox, false, true, true, true);
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Back(ref Message m)
            {
                if (!_hexBox._byteProvider.SupportsDeleteBytes()) return true;

                if (_hexBox.ReadOnly || !_hexBox.EnableDelete) return true;

                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;
                int cPos = _hexBox._byteCharacterPos;

                long startDelete = (cPos == 0 && selLen == 0) ? pos - 1 : pos;
                if (startDelete < 0 && selLen < 1) return true;

                long bytesToDelete = (selLen > 0) ? selLen : 1;
                _hexBox._byteProvider.DeleteBytes(Math.Max(0, startDelete), bytesToDelete);
                _hexBox.UpdateScrollSize();

                if (selLen == 0) PerformPosMoveLeftByte();

                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Delete(ref Message m)
            {
                if (!_hexBox._byteProvider.SupportsDeleteBytes()) return true;

                if (_hexBox.ReadOnly || !_hexBox.EnableDelete) return true;

                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;

                if (pos >= _hexBox._byteProvider.Length) return true;

                long bytesToDelete = (selLen > 0) ? selLen : 1;
                _hexBox._byteProvider.DeleteBytes(pos, bytesToDelete);

                _hexBox.UpdateScrollSize();
                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Home(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos < 1) return true;

                pos = 0;
                cPos = 0;
                _hexBox.SetPosition(pos, cPos);

                _hexBox.ScrollByteIntoView();
                _hexBox.UpdateCaret();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_End(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos >= _hexBox._byteProvider.Length - 1) return true;

                pos = _hexBox._byteProvider.Length;
                cPos = 0;
                _hexBox.SetPosition(pos, cPos);

                _hexBox.ScrollByteIntoView();
                _hexBox.UpdateCaret();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftShiftKey(ref Message m)
            {
                if (_mouseDown) return true;
                if (_shiftDown) return true;

                _shiftDown = true;

                if (_hexBox._selectionLength > 0) return true;

                _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlC(ref Message m)
            {
                _hexBox.Copy(_hexBox.KeyDownControlCContentType == StringContentType.Hex);
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlX(ref Message m)
            {
                _hexBox.Cut();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlV(ref Message m)
            {
                _hexBox.Paste();
                return true;
            }

            #endregion

            #region PreProcessWmChar methods
            public virtual bool PreProcessWmChar(ref Message m)
            {
                if (Control.ModifierKeys == Keys.Control) return _hexBox.BasePreProcessMessage(ref m);

                bool sWrite = _hexBox._byteProvider.SupportsWriteByte();
                bool sInsert = _hexBox._byteProvider.SupportsInsertBytes();
                bool sDelete = _hexBox._byteProvider.SupportsDeleteBytes();

                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;
                int cPos = _hexBox._byteCharacterPos;

                if (!sWrite && pos != _hexBox._byteProvider.Length || !sInsert && pos == _hexBox._byteProvider.Length) return _hexBox.BasePreProcessMessage(ref m);

                char keyChar = (char)m.WParam.ToInt32();

                if (Uri.IsHexDigit(keyChar))
                {
                    if (RaiseKeyPress(keyChar)) return true;

                    if (_hexBox.ReadOnly) return true;

                    bool isInsertMode = (pos == _hexBox._byteProvider.Length); //Has the current position reached the end of byteProvider

                    // do insert when insertActive = true
                    if (!isInsertMode && sInsert && _hexBox.InsertActive && cPos == 0) isInsertMode = true;

                    if (_hexBox.EnableCut && _hexBox.EnableDelete && _hexBox.EnablePaste && sDelete && sInsert && selLen > 0)
                    {
                        _hexBox._byteProvider.DeleteBytes(pos, selLen);
                        isInsertMode = true;
                        cPos = 0;
                        _hexBox.SetPosition(pos, cPos);
                    }

                    _hexBox.ReleaseSelection();

                    byte currentByte = isInsertMode ? (byte)0 : _hexBox._byteProvider.ReadByte(pos);

                    string sCb = currentByte.ToString("X", System.Threading.Thread.CurrentThread.CurrentCulture);
                    if (sCb.Length == 1) sCb = "0" + sCb;

                    string sNewCb = keyChar.ToString();
                    if (cPos == 0) sNewCb += sCb.Substring(1, 1);
                    else sNewCb = sCb.Substring(0, 1) + sNewCb;
                    byte newcb = byte.Parse(sNewCb, System.Globalization.NumberStyles.AllowHexSpecifier, System.Threading.Thread.CurrentThread.CurrentCulture);

                    if (isInsertMode) _hexBox._byteProvider.InsertBytes(pos, new byte[] { newcb });
                    else _hexBox._byteProvider.WriteByte(pos, newcb);

                    PerformPosMoveRight();

                    _hexBox.Invalidate();
                    return true;
                }
                else return _hexBox.BasePreProcessMessage(ref m);
            }

            protected bool RaiseKeyPress(char keyChar)
            {
                KeyPressEventArgs e = new KeyPressEventArgs(keyChar);
                _hexBox.OnKeyPress(e);
                return e.Handled;
            }
            #endregion

            #region PreProcessWmKeyUp methods
            public virtual bool PreProcessWmKeyUp(ref Message m)
            {
                System.Diagnostics.Debug.WriteLine("PreProcessWmKeyUp(ref Message m)", "KeyInterpreter");

                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                switch (keyData)
                {
                    case Keys.ShiftKey:
                    case Keys.Insert:
                        if (RaiseKeyUp(keyData)) return true;
                        break;
                }

                switch (keyData)
                {
                    case Keys.ShiftKey:
                        _shiftDown = false;
                        return true;
                    case Keys.Insert:
                        return PreProcessWmKeyUp_Insert(ref m);
                    default:
                        return _hexBox.BasePreProcessMessage(ref m);
                }
            }

            protected virtual bool PreProcessWmKeyUp_Insert(ref Message m)
            {
                _hexBox.InsertActive = !_hexBox.InsertActive;
                return true;
            }

            protected bool RaiseKeyUp(Keys keyData)
            {
                KeyEventArgs e = new KeyEventArgs(keyData);
                _hexBox.OnKeyUp(e);
                return e.Handled;
            }
            #endregion

            #region Misc
            Dictionary<Keys, MessageDelegate> MessageHandlers
            {
                get
                {
                    if (_messageHandlers == null)
                    {
                        _messageHandlers = new Dictionary<Keys, MessageDelegate>();
                        _messageHandlers.Add(Keys.Left,                  new MessageDelegate(PreProcessWmKeyDown_Left));          // move left
                        _messageHandlers.Add(Keys.Up,                    new MessageDelegate(PreProcessWmKeyDown_Up));            // move up
                        _messageHandlers.Add(Keys.Right,                 new MessageDelegate(PreProcessWmKeyDown_Right));         // move right
                        _messageHandlers.Add(Keys.Down,                  new MessageDelegate(PreProcessWmKeyDown_Down));          // move down
                        _messageHandlers.Add(Keys.PageUp,                new MessageDelegate(PreProcessWmKeyDown_PageUp));        // move pageup
                        _messageHandlers.Add(Keys.PageDown,              new MessageDelegate(PreProcessWmKeyDown_PageDown));      // move page down
                        _messageHandlers.Add(Keys.Left  | Keys.Shift,    new MessageDelegate(PreProcessWmKeyDown_ShiftLeft));     // move left with selection
                        _messageHandlers.Add(Keys.Up    | Keys.Shift,    new MessageDelegate(PreProcessWmKeyDown_ShiftUp));       // move up with selection
                        _messageHandlers.Add(Keys.Right | Keys.Shift,    new MessageDelegate(PreProcessWmKeyDown_ShiftRight));    // move right with selection
                        _messageHandlers.Add(Keys.Down  | Keys.Shift,    new MessageDelegate(PreProcessWmKeyDown_ShiftDown));     // move down with selection
                        _messageHandlers.Add(Keys.Tab,                   new MessageDelegate(PreProcessWmKeyDown_Tab));           // switch to string view
                        _messageHandlers.Add(Keys.Back,                  new MessageDelegate(PreProcessWmKeyDown_Back));          // back
                        _messageHandlers.Add(Keys.Delete,                new MessageDelegate(PreProcessWmKeyDown_Delete));        // delete
                        _messageHandlers.Add(Keys.Home,                  new MessageDelegate(PreProcessWmKeyDown_Home));          // move to home
                        _messageHandlers.Add(Keys.End,                   new MessageDelegate(PreProcessWmKeyDown_End));           // move to end
                        _messageHandlers.Add(Keys.ShiftKey | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftShiftKey)); // begin selection process
                        _messageHandlers.Add(Keys.C | Keys.Control,      new MessageDelegate(PreProcessWmKeyDown_ControlC));      // copy 
                        _messageHandlers.Add(Keys.X | Keys.Control,      new MessageDelegate(PreProcessWmKeyDown_ControlX));      // cut
                        _messageHandlers.Add(Keys.V | Keys.Control,      new MessageDelegate(PreProcessWmKeyDown_ControlV));      // paste
                    }
                    return _messageHandlers;
                }
            }

            protected virtual bool PerformPosMoveLeft()
            {
                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;
                int cPos = _hexBox._byteCharacterPos;

                if (selLen != 0)
                {
                    cPos = 0;
                    _hexBox.SetPosition(pos, cPos);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    if (pos == 0 && cPos == 0) return true;

                    if (cPos > 0) cPos--;
                    else
                    {
                        pos = Math.Max(0, pos - 1);
                        cPos++;
                    }

                    _hexBox.SetPosition(pos, cPos);

                    if (pos < _hexBox._startByte) _hexBox.PerformScrollLineUp();
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                }

                _hexBox.ScrollByteIntoView();
                return true;
            }

            protected virtual bool PerformPosMoveRight()
            {
                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;
                int cPos = _hexBox._byteCharacterPos;

                if (selLen != 0)
                {
                    pos += selLen;
                    cPos = 0;
                    _hexBox.SetPosition(pos, cPos);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    if (!(pos == _hexBox._byteProvider.Length && cPos == 0))
                    {
                        if (cPos > 0)
                        {
                            pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
                            cPos = 0;
                        }
                        else cPos++;

                        _hexBox.SetPosition(pos, cPos);

                        if (pos > _hexBox._endByte - 1) _hexBox.PerformScrollLineDown();
                        _hexBox.UpdateCaret();
                        _hexBox.Invalidate();
                    }
                }

                _hexBox.ScrollByteIntoView();
                return true;
            }

            protected virtual bool PerformPosMoveLeftByte()
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos == 0) return true;

                pos = Math.Max(0, pos - 1);
                cPos = 0;

                _hexBox.SetPosition(pos, cPos);

                if (pos < _hexBox._startByte) _hexBox.PerformScrollLineUp();
                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PerformPosMoveRightByte()
            {
                long pos = _hexBox._bytePos;
                int cPos = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length) return true; //Current position reached the end of byteProvider

                pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
                cPos = 0;

                _hexBox.SetPosition(pos, cPos);

                if (pos > _hexBox._endByte - 1) _hexBox.PerformScrollLineDown();
                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.Invalidate();

                return true;
            }

            public virtual PointF GetCaretPointF(long byteIndex)
            {
                System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "KeyInterpreter");

                return _hexBox.GetBytePointF(byteIndex);
            }

            protected virtual BytePositionInfo GetBytePositionInfo(Point p) => _hexBox.GetHexBytePositionInfo(p);
            #endregion
        }
        #endregion

        #region StringKeyInterpreter class
        /// <summary>
        /// Handles user input such as mouse and keyboard input during string view edit
        /// </summary>
        class StringKeyInterpreter : KeyInterpreter
        {
            #region Ctors
            public StringKeyInterpreter(HexBox hexBox) : base(hexBox) => _hexBox._byteCharacterPos = 0;
            #endregion

            #region PreProcessWmKeyDown methods
            public override bool PreProcessWmKeyDown(ref Message m)
            {
                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                switch (keyData)
                {
                    case Keys.Tab | Keys.Shift:
                    case Keys.Tab:
                        if (RaiseKeyDown(keyData)) return true;
                        break;
                }

                switch (keyData)
                {
                    case Keys.Tab | Keys.Shift:
                        return PreProcessWmKeyDown_ShiftTab(ref m);
                    case Keys.Tab:
                        return PreProcessWmKeyDown_Tab(ref m);
                    default:
                        return base.PreProcessWmKeyDown(ref m);
                }
            }

            protected override bool PreProcessWmKeyDown_Left(ref Message m) => PerformPosMoveLeftByte();

            protected override bool PreProcessWmKeyDown_Right(ref Message m) => PerformPosMoveRightByte();

            #endregion

            #region PreProcessWmChar methods
            public override bool PreProcessWmChar(ref Message m)
            {
                if (Control.ModifierKeys == Keys.Control) return _hexBox.BasePreProcessMessage(ref m);

                bool sWrite = _hexBox._byteProvider.SupportsWriteByte();
                bool sInsert = _hexBox._byteProvider.SupportsInsertBytes();
                bool sDelete = _hexBox._byteProvider.SupportsDeleteBytes();

                long pos = _hexBox._bytePos;
                long selLen = _hexBox._selectionLength;
                int cPos = _hexBox._byteCharacterPos;

                if (!sWrite && pos != _hexBox._byteProvider.Length || !sInsert && pos == _hexBox._byteProvider.Length) return _hexBox.BasePreProcessMessage(ref m);

                char keyChar = (char)m.WParam.ToInt32();

                if (RaiseKeyPress(keyChar)) return true;

                if (_hexBox.ReadOnly) return true;

                bool isInsertMode = (pos == _hexBox._byteProvider.Length); //Has the current position reached the end of byteProvider

                // do insert when insertActive = true
                if (!isInsertMode && sInsert && _hexBox.InsertActive) isInsertMode = true;

                if (_hexBox.EnableCut && _hexBox.EnableDelete && _hexBox.EnablePaste && sDelete && sInsert && selLen > 0)
                {
                    _hexBox._byteProvider.DeleteBytes(pos, selLen);
                    isInsertMode = true;
                    cPos = 0;
                    _hexBox.SetPosition(pos, cPos);
                }

                _hexBox.ReleaseSelection();

                byte b = _hexBox.ByteCharConverter.ToByte(keyChar);
                if (isInsertMode) _hexBox._byteProvider.InsertBytes(pos, new byte[] { b });
                else _hexBox._byteProvider.WriteByte(pos, b);

                PerformPosMoveRightByte();
                _hexBox.Invalidate();

                return true;
            }
            #endregion

            #region Misc
            public override PointF GetCaretPointF(long byteIndex)
            {
                System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "StringKeyInterpreter");

                Point gp = _hexBox.GetGridBytePoint(byteIndex);
                return _hexBox.GetByteStringPointF(gp);
            }

            protected override BytePositionInfo GetBytePositionInfo(Point p) => _hexBox.GetStringBytePositionInfo(p);
            #endregion
        }
        #endregion

        #region Fields
        /// <summary>
        /// Contains the hole content bounds of all text
        /// </summary>
        Rectangle _recContent;
        /// <summary>
        /// Contains the line info bounds
        /// </summary>
        Rectangle _recLineInfo;
        /// <summary>
        /// Contains the column info header rectangle bounds
        /// </summary>
        Rectangle _recColumnInfo;
        /// <summary>
        /// Contains the hex data bounds
        /// </summary>
        Rectangle _recHex;
        /// <summary>
        /// Contains the string view bounds
        /// </summary>
        Rectangle _recStringView;

        /// <summary>
        /// Contains string format information for text drawing
        /// </summary>
        StringFormat _stringFormat;
        /// <summary>
        /// Contains the maximum of visible horizontal bytes
        /// </summary>
        int _iHexMaxHBytes;
        /// <summary>
        /// Contains the maximum of visible vertical bytes
        /// </summary>
        int _iHexMaxVBytes;
        /// <summary>
        /// Contains the maximum of visible bytes.
        /// </summary>
        int _iHexMaxBytes;

        /// <summary>
        /// Contains the scroll bars minimum value
        /// </summary>
        long _scrollVmin;
        /// <summary>
        /// Contains the scroll bars maximum value
        /// </summary>
        long _scrollVmax;
        /// <summary>
        /// Contains a vertical scroll
        /// </summary>
        VScrollBar _vScrollBar;
        /// <summary>
        /// Contains a timer for thumbtrack scrolling
        /// </summary>
        Timer _thumbTrackTimer = new Timer();
        /// <summary>
        /// Contains the thumbtrack scrolling position
        /// </summary>
        long _thumbTrackPosition;
        /// <summary>
        /// Contains the thumptrack delay for scrolling in milliseconds.
        /// </summary>
        const int THUMPTRACKDELAY = 50;
        /// <summary>
        /// Contains the Enviroment.TickCount of the last refresh
        /// </summary>
        int _lastThumbtrack;
        /// <summary>
        /// Contains the border's left shift
        /// </summary>
        int _recBorderLeft = SystemInformation.Border3DSize.Width;
        /// <summary>
        /// Contains the border's right shift
        /// </summary>
        int _recBorderRight = SystemInformation.Border3DSize.Width;
        /// <summary>
        /// Contains the border's top shift
        /// </summary>
        int _recBorderTop = SystemInformation.Border3DSize.Height;
        /// <summary>
        /// Contains the border bottom shift
        /// </summary>
        int _recBorderBottom = SystemInformation.Border3DSize.Height;

        /// <summary>
        /// Contains the index of the first visible byte
        /// </summary>
        long _startByte;
        /// <summary>
        /// Contains the index of the last visible byte
        /// </summary>
        long _endByte;

        /// <summary>
        /// Contains the current byte position
        /// </summary>
        long _bytePos = -1;
        /// <summary>
        /// Contains the current char position in one byte
        /// </summary>
        /// <example>
        /// "1A"
        /// "1" = char position of 0
        /// "A" = char position of 1
        /// </example>
        int _byteCharacterPos;

        /// <summary>
        /// Contains string format information for hex values
        /// </summary>
        string _hexStringFormat = "X";

        /// <summary>
        /// Contains the current key interpreter
        /// </summary>
        IKeyInterpreter _keyInterpreter;
        /// <summary>
        /// Contains an empty key interpreter without functionality
        /// </summary>
        EmptyKeyInterpreter _eki;
        /// <summary>
        /// Contains the default key interpreter
        /// </summary>
        KeyInterpreter _ki;
        /// <summary>
        /// Contains the string key interpreter
        /// </summary>
        StringKeyInterpreter _ski;

        /// <summary>
        /// Contains True if caret is visible
        /// </summary>
        bool _caretVisible;

        /// <summary>
        /// Contains true, if the find (Find method) should be aborted.
        /// </summary>
        bool _abortFind;

        /// <summary>
        /// Contains a state value about Insert or Write mode. When this value is true and the ByteProvider SupportsInsert is true bytes are inserted instead of overridden.
        /// </summary>
        bool _insertActive;

        /// <summary>
        /// Record the position of the changed bytes value.
        /// </summary>
        HashSet<long> changedPosSet;
        /// <summary>
        /// Record the position of finish changed bytes value.
        /// </summary>
        HashSet<long> changedFinishPosSet;
        #endregion

        #region Events
        /// <summary>
        /// Occurs, when the value of InsertActive property has changed.
        /// </summary>
        [Description("Occurs, when the value of InsertActive property has changed.")]
        public event EventHandler InsertActiveChanged;
        /// <summary>
        /// Occurs, when the value of ReadOnly property has changed.
        /// </summary>
        [Description("Occurs, when the value of ReadOnly property has changed.")]
        public event EventHandler ReadOnlyChanged;
        /// <summary>
        /// Occurs, when the value of ByteProvider property has changed.
        /// </summary>
        [Description("Occurs, when the value of ByteProvider property has changed.")]
        public event EventHandler ByteProviderChanged;
        /// <summary>
        /// Occurs, when the value of SelectionStart property has changed.
        /// </summary>
        [Description("Occurs, when the value of SelectionStart property has changed.")]
        public event EventHandler SelectionStartChanged;
        /// <summary>
        /// Occurs, when the value of SelectionLength property has changed.
        /// </summary>
        [Description("Occurs, when the value of SelectionLength property has changed.")]
        public event EventHandler SelectionLengthChanged;
        /// <summary>
        /// Occurs, when the value of LineInfoVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of LineInfoVisible property has changed.")]
        public event EventHandler LineInfoVisibleChanged;
        /// <summary>
        /// Occurs, when the value of ColumnInfoVisibleChanged property has changed.
        /// </summary>
        [Description("Occurs, when the value of ColumnInfoVisibleChanged property has changed.")]
        public event EventHandler ColumnInfoVisibleChanged;
        /// <summary>
        /// Occurs, when the value of GroupSeparatorVisibleChanged property has changed.
        /// </summary>
        [Description("Occurs, when the value of GroupSeparatorVisibleChanged property has changed.")]
        public event EventHandler GroupSeparatorVisibleChanged;
        /// <summary>
        /// Occurs, when the value of StringViewVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of StringViewVisible property has changed.")]
        public event EventHandler StringViewVisibleChanged;
        /// <summary>
        /// Occurs, when the value of BorderStyle property has changed.
        /// </summary>
        [Description("Occurs, when the value of BorderStyle property has changed.")]
        public event EventHandler BorderStyleChanged;
        /// <summary>
        /// Occurs, when the value of ColumnWidth property has changed.
        /// </summary>
        [Description("Occurs, when the value of GroupSize property has changed.")]
        public event EventHandler GroupSizeChanged;
        /// <summary>
        /// Occurs, when the value of BytesPerLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of BytesPerLine property has changed.")]
        public event EventHandler BytesPerLineChanged;
        /// <summary>
        /// Occurs, when the value of UseFixedBytesPerLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of UseFixedBytesPerLine property has changed.")]
        public event EventHandler UseFixedBytesPerLineChanged;
        /// <summary>
        /// Occurs, when the value of VScrollBarVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of VScrollBarVisible property has changed.")]
        public event EventHandler VScrollBarVisibleChanged;
        /// <summary>
        /// Occurs, when the value of HexCasing property has changed.
        /// </summary>
        [Description("Occurs, when the value of HexCasing property has changed.")]
        public event EventHandler HexCasingChanged;
        /// <summary>
        /// Occurs, when the value of HorizontalByteCount property has changed.
        /// </summary>
        [Description("Occurs, when the value of HorizontalByteCount property has changed.")]
        public event EventHandler HorizontalByteCountChanged;
        /// <summary>
        /// Occurs, when the value of VerticalByteCount property has changed.
        /// </summary>
        [Description("Occurs, when the value of VerticalByteCount property has changed.")]
        public event EventHandler VerticalByteCountChanged;
        /// <summary>
        /// Occurs, when the value of CurrentLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of CurrentLine property has changed.")]
        public event EventHandler CurrentLineChanged;
        /// <summary>
        /// Occurs, when the value of CurrentPositionInLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of CurrentPositionInLine property has changed.")]
        public event EventHandler CurrentPositionInLineChanged;
        /// <summary>
        /// Occurs, when Copy method was invoked and ClipBoardData changed.
        /// </summary>
        [Description("Occurs, when Copy method was invoked and ClipBoardData changed.")]
        public event EventHandler Copied;
        /// <summary>
        /// Occurs, when the CharSize property has changed
        /// </summary>
        [Description("Occurs, when the CharSize property has changed")]
        public event EventHandler CharSizeChanged;
        /// <summary>
        /// Occurs, when the RequiredWidth property changes
        /// </summary>
        [Description("Occurs, when the RequiredWidth property changes")]
        public event EventHandler RequiredWidthChanged;
        #endregion

        #region Ctors
        /// <summary>
        /// Initializes a new instance of a HexBox class.
        /// </summary>
        public HexBox()
        {
            changedPosSet = new HashSet<long>();
            changedFinishPosSet = new HashSet<long>();
            _vScrollBar = new VScrollBar();
            _vScrollBar.Scroll += new ScrollEventHandler(_vScrollBar_Scroll);

            BuiltInContextMenu = new BuiltInContextMenu(this);

            BackColor = Color.White;
            Font = SystemFonts.MessageBoxFont;
            _stringFormat = new StringFormat(StringFormat.GenericTypographic);
            _stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces;

            ActivateEmptyKeyInterpreter();

            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);

            _thumbTrackTimer.Interval = 50;
            _thumbTrackTimer.Tick += new EventHandler(PerformScrollThumbTrack);
        }
        #endregion

        #region Scroll methods
        void _vScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            switch (e.Type)
            {
                case ScrollEventType.Last:
                    break;
                case ScrollEventType.EndScroll:
                    break;
                case ScrollEventType.SmallIncrement:
                    PerformScrollLineDown();
                    break;
                case ScrollEventType.SmallDecrement:
                    PerformScrollLineUp();
                    break;
                case ScrollEventType.LargeIncrement:
                    PerformScrollPageDown();
                    break;
                case ScrollEventType.LargeDecrement:
                    PerformScrollPageUp();
                    break;
                case ScrollEventType.ThumbPosition:
                    long lPos = FromScrollPos(e.NewValue);
                    PerformScrollThumpPosition(lPos);
                    break;
                case ScrollEventType.ThumbTrack:
                    // to avoid performance problems use a refresh delay implemented with a timer
                    if (_thumbTrackTimer.Enabled) _thumbTrackTimer.Enabled = false;// stop old timer

                    // perform scroll immediately only if last refresh is very old
                    int currentThumbTrack = System.Environment.TickCount;
                    if (currentThumbTrack - _lastThumbtrack > THUMPTRACKDELAY)
                    {
                        PerformScrollThumbTrack(null, null);
                        _lastThumbtrack = currentThumbTrack;
                        break;
                    }

                    // start thumbtrack timer 
                    _thumbTrackPosition = FromScrollPos(e.NewValue);
                    _thumbTrackTimer.Enabled = true;
                    break;
                case ScrollEventType.First:
                    break;
                default:
                    break;
            }

            e.NewValue = ToScrollPos(ScrollVpos);
        }

        /// <summary>
        /// Performs the thumbtrack scrolling after an delay.
        /// </summary>
        void PerformScrollThumbTrack(object sender, EventArgs e)
        {
            _thumbTrackTimer.Enabled = false;
            PerformScrollThumpPosition(_thumbTrackPosition);
            _lastThumbtrack = Environment.TickCount;
        }

        void UpdateScrollSize()
        {
            System.Diagnostics.Debug.WriteLine("UpdateScrollSize()", "HexBox");

            // calc scroll bar info
            if (VScrollBarVisible && _byteProvider != null && _byteProvider.Length > 0 && _iHexMaxHBytes != 0)
            {
                long scrollmax = (long)Math.Ceiling((double)(_byteProvider.Length + 1) / (double)_iHexMaxHBytes - (double)_iHexMaxVBytes);
                scrollmax = Math.Max(0, scrollmax);

                long scrollpos = _startByte / _iHexMaxHBytes;

                if (scrollmax < _scrollVmax)
                {
                    /* Data size has been decreased. */
                    if (ScrollVpos == _scrollVmax) PerformScrollLineUp(); /* Scroll one line up if we at bottom. */
                }

                if (scrollmax == _scrollVmax && scrollpos == ScrollVpos) return;

                _scrollVmin = 0;
                _scrollVmax = scrollmax;
                ScrollVpos = Math.Min(scrollpos, scrollmax);
                UpdateVScroll();
            }
            else if (VScrollBarVisible)
            {
                // disable scroll bar
                _scrollVmin = 0;
                _scrollVmax = 0;
                ScrollVpos = 0;
                UpdateVScroll();
            }
        }

        void UpdateVScroll()
        {
            System.Diagnostics.Debug.WriteLine("UpdateVScroll()", "HexBox");

            int max = ToScrollMax(_scrollVmax);

            if (max > 0)
            {
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = max;
                _vScrollBar.Value = ToScrollPos(ScrollVpos);
                _vScrollBar.Visible = true;
            }
            else _vScrollBar.Visible = false;
        }

        int ToScrollPos(long value)
        {
            int max = 65535;

            if (_scrollVmax < max) return (int)value;
            else
            {
                double valperc = (double)value / (double)_scrollVmax * (double)100;
                int res = (int)Math.Floor((double)max / (double)100 * valperc);
                res = (int)Math.Max(_scrollVmin, res);
                res = (int)Math.Min(_scrollVmax, res);
                return res;
            }
        }

        long FromScrollPos(int value)
        {
            int max = 65535;
            if (_scrollVmax < max) return (long)value;
            else
            {
                double valperc = (double)value / (double)max * (double)100;
                long res = (int)Math.Floor((double)_scrollVmax / (double)100 * valperc);
                return res;
            }
        }

        int ToScrollMax(long value)
        {
            long max = 65535;
            if (value > max) return (int)max;
            else return (int)value;
        }

        void PerformScrollLines(int lines)
        {
            long pos;
            if (lines > 0) pos = Math.Min(_scrollVmax, ScrollVpos + lines);
            else if (lines < 0) pos = Math.Max(_scrollVmin, ScrollVpos + lines);
            else return;

            PerformScrollToLine(pos);
        }

        void PerformScrollLineDown() => PerformScrollLines(1);

        void PerformScrollLineUp() => PerformScrollLines(-1);

        void PerformScrollPageDown() => PerformScrollLines(_iHexMaxVBytes);

        void PerformScrollPageUp() => PerformScrollLines(-_iHexMaxVBytes);

        void PerformScrollThumpPosition(long pos)
        {
            // Bug fix: Scroll to end, do not scroll to end
            int difference = (_scrollVmax > 65535) ? 10 : 9;

            if (ToScrollPos(pos) == ToScrollMax(_scrollVmax) - difference)
                pos = _scrollVmax;
            // End Bug fix

            PerformScrollToLine(pos);
        }

        /// <summary>
        /// perform scroll to line
        /// </summary>
        public void PerformScrollToLine(long pos)
        {
            if (pos < _scrollVmin || pos > _scrollVmax || pos == ScrollVpos) return;

            ScrollVpos = pos;

            UpdateVScroll();
            UpdateVisibilityBytes();
            UpdateCaret();
            Invalidate();
        }

        /// <summary>
        /// Scrolls the selection start byte into view
        /// </summary>
        public void ScrollByteIntoView()
        {
            System.Diagnostics.Debug.WriteLine("ScrollByteIntoView()", "HexBox");

            ScrollByteIntoView(_bytePos);
        }

        /// <summary>
        /// Scrolls the specific byte into view
        /// </summary>
        /// <param name="index">the index of the byte</param>
        public void ScrollByteIntoView(long index)
        {
            System.Diagnostics.Debug.WriteLine("ScrollByteIntoView(long index)", "HexBox");

            if (_byteProvider == null || _keyInterpreter == null) return;

            if (index < _startByte)
            {
                long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
                PerformScrollThumpPosition(line);
            }
            else if (index > _endByte)
            {
                long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
                line -= _iHexMaxVBytes - 1;
                PerformScrollThumpPosition(line);
            }
        }
        #endregion

        #region Selection methods
        void ReleaseSelection()
        {
            System.Diagnostics.Debug.WriteLine("ReleaseSelection()", "HexBox");

            if (_selectionLength == 0) return;

            _selectionLength = 0;
            OnSelectionLengthChanged(EventArgs.Empty);

            if (!_caretVisible) CreateCaret();
            else UpdateCaret();

            Invalidate();
        }

        /// <summary>
        /// Returns true if Select method could be invoked.
        /// </summary>
        public bool CanSelectAll()
        {
            if (!Enabled) return false;
            if (_byteProvider == null) return false;

            return true;
        }

        /// <summary>
        /// Selects all bytes.
        /// </summary>
        public void SelectAll()
        {
            if (ByteProvider == null) return;

            Select(0, ByteProvider.Length);
        }

        /// <summary>
        /// Selects the hex box.
        /// </summary>
        /// <param name="start">the start index of the selection</param>
        /// <param name="length">the length of the selection</param>
        public void Select(long start, long length)
        {
            if (ByteProvider == null) return;
            if (!Enabled) return;

            InternalSelect(start, length);
            ScrollByteIntoView();
        }

        void InternalSelect(long start, long length)
        {
            long pos = start;
            long selLen = length;
            int cPos = 0;

            if (selLen > 0 && _caretVisible) DestroyCaret();
            else if (selLen == 0 && !_caretVisible) CreateCaret();

            SetPosition(pos, cPos);
            SetSelectionLength(selLen);

            UpdateCaret();
            Invalidate();
        }
        #endregion

        #region Key interpreter methods
        void ActivateEmptyKeyInterpreter()
        {
            if (_eki == null) _eki = new EmptyKeyInterpreter(this);

            if (_eki == _keyInterpreter) return;

            if (_keyInterpreter != null) _keyInterpreter.Deactivate();

            _keyInterpreter = _eki;
            _keyInterpreter.Activate();
        }

        void ActivateKeyInterpreter()
        {
            if (_ki == null) _ki = new KeyInterpreter(this);

            if (_ki == _keyInterpreter) return;

            if (_keyInterpreter != null) _keyInterpreter.Deactivate();

            _keyInterpreter = _ki;
            _keyInterpreter.Activate();
        }

        void ActivateStringKeyInterpreter()
        {
            if (_ski == null) _ski = new StringKeyInterpreter(this);

            if (_ski == _keyInterpreter) return;

            if (_keyInterpreter != null) _keyInterpreter.Deactivate();

            _keyInterpreter = _ski;
            _keyInterpreter.Activate();
        }
        #endregion

        #region Caret methods
        void CreateCaret()
        {
            if (_byteProvider == null || _keyInterpreter == null || _caretVisible || !Focused) return;

            System.Diagnostics.Debug.WriteLine("CreateCaret()", "HexBox");

            // define the caret width depending on InsertActive mode
            int caretWidth = (InsertActive) ? 1 : (int)CharSize.Width;
            int caretHeight = (int)CharSize.Height;
            Caret.Create(Handle, IntPtr.Zero, caretWidth, caretHeight);

            UpdateCaret();

            Caret.Show(Handle);

            _caretVisible = true;
        }

        void UpdateCaret()
        {
            if (_byteProvider == null || _keyInterpreter == null) return;

            System.Diagnostics.Debug.WriteLine("UpdateCaret()", "HexBox");

            long byteIndex = _bytePos - _startByte;
            PointF p = _keyInterpreter.GetCaretPointF(byteIndex);
            p.X += _byteCharacterPos * CharSize.Width;
            Caret.SetPos((int)p.X, (int)p.Y);
        }

        void DestroyCaret()
        {
            if (!_caretVisible) return;

            System.Diagnostics.Debug.WriteLine("DestroyCaret()", "HexBox");

            Caret.Destroy();
            _caretVisible = false;
        }

        void SetCaretPosition(Point p)
        {
            System.Diagnostics.Debug.WriteLine("SetCaretPosition()", "HexBox");

            if (_byteProvider == null || _keyInterpreter == null) return;

            long pos = _bytePos;
            int cPos = _byteCharacterPos;

            if (_recHex.Contains(p))
            {
                BytePositionInfo bpi = GetHexBytePositionInfo(p);
                pos = bpi.Index;
                cPos = bpi.CharacterPosition;

                SetPosition(pos, cPos);

                ActivateKeyInterpreter();
                UpdateCaret();
                Invalidate();
            }
            else if (_recStringView.Contains(p))
            {
                BytePositionInfo bpi = GetStringBytePositionInfo(p);
                pos = bpi.Index;
                cPos = bpi.CharacterPosition;

                SetPosition(pos, cPos);

                ActivateStringKeyInterpreter();
                UpdateCaret();
                Invalidate();
            }
        }

        BytePositionInfo GetHexBytePositionInfo(Point p)
        {
            System.Diagnostics.Debug.WriteLine("GetHexBytePositionInfo()", "HexBox");

            long bytePos;
            int byteCharacterPos;

            float x = ((float)(p.X - _recHex.X) / CharSize.Width);
            float y = ((float)(p.Y - _recHex.Y) / CharSize.Height);
            int iX = (int)x;
            int iY = (int)y;

            int hPos = iX / (ByteGroupingSize == 1 ? 3 : 2);
            byteCharacterPos = (iX % (ByteGroupingSize == 1 ? 3 : 2));

            //float bytePointFX = (p.X - _recHex.X - ByteGroupingSize * CharSize.Width * (hPos / ByteGroupingSize)) / ((ByteGroupingSize == 1 ? 3 : 2) * CharSize.Width);
            var emptySize = ByteGroupingSize * 3 - ByteGroupingSize * 2;
            var rank1 = iX / (ByteGroupingSize * 3);
            var rank2 = (iX + emptySize) / (ByteGroupingSize * 3);
            var remainder = (iX + emptySize) % (ByteGroupingSize * 3);
            if (ByteGroupingSize != 1)
            {
                hPos -= rank1 * ByteGroupingSize / 2;
                if (rank1 != rank2)
                {
                    hPos -= remainder / 2;
                    byteCharacterPos = 0;
                }
            }

            bytePos = Math.Min(_byteProvider.Length, _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos);

            if (byteCharacterPos > 1) byteCharacterPos = 1;

            if (bytePos == _byteProvider.Length) byteCharacterPos = 0;

            if (bytePos < 0) return new BytePositionInfo(0, 0);

            return new BytePositionInfo(bytePos, byteCharacterPos);
        }

        BytePositionInfo GetStringBytePositionInfo(Point p)
        {
            System.Diagnostics.Debug.WriteLine("GetStringBytePositionInfo()", "HexBox");

            long bytePos;
            int byteCharacterPos;

            float x = ((float)(p.X - _recStringView.X) / CharSize.Width);
            float y = ((float)(p.Y - _recStringView.Y) / CharSize.Height);
            int iX = (int)x;
            int iY = (int)y;

            int hPos = iX + 1;

            bytePos = Math.Min(_byteProvider.Length, _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos - 1);
            byteCharacterPos = 0;

            if (bytePos < 0) return new BytePositionInfo(0, 0);

            return new BytePositionInfo(bytePos, byteCharacterPos);
        }
        #endregion

        #region PreProcessMessage methods
        /// <summary>
        /// Preprocesses windows messages.
        /// </summary>
        /// <param name="m">the message to process.</param>
        /// <returns>true, if the message was processed</returns>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true), SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
        public override bool PreProcessMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeMethods.WM_KEYDOWN:
                    return _keyInterpreter.PreProcessWmKeyDown(ref m);
                case NativeMethods.WM_CHAR:
                    if (!changedPosSet.Contains(_bytePos)) changedPosSet.Add(_bytePos);
                    return _keyInterpreter.PreProcessWmChar(ref m);
                case NativeMethods.WM_KEYUP:
                    return _keyInterpreter.PreProcessWmKeyUp(ref m);
                default:
                    return base.PreProcessMessage(ref m);
            }
        }

        bool BasePreProcessMessage(ref Message m) => base.PreProcessMessage(ref m);
        #endregion

        #region Find methods
        /// <summary>
        /// Searches the current ByteProvider
        /// </summary>
        /// <param name="options">contains all find options</param>
        /// <returns>the SelectionStart property value if find was successfull or
        /// -1 if there is no match
        /// -2 if Find was aborted.</returns>
        public long Find(FindOptions options)
        {
            var startIndex = SelectionStart;
            int match = 0;

            byte[] buffer1 = null;
            byte[] buffer2 = null;
            if (options.Type == FindType.Text && options.MatchCase)
            {
                if (options.FindBuffer == null || options.FindBuffer.Length == 0) throw new ArgumentException("FindBuffer can not be null when Type: Text and MatchCase: false");
                buffer1 = options.FindBuffer;
            }
            else if (options.Type == FindType.Text && !options.MatchCase)
            {
                if (options.FindBufferLowerCase == null || options.FindBufferLowerCase.Length == 0) throw new ArgumentException("FindBufferLowerCase can not be null when Type is Text and MatchCase is true");
                if (options.FindBufferUpperCase == null || options.FindBufferUpperCase.Length == 0) throw new ArgumentException("FindBufferUpperCase can not be null when Type is Text and MatchCase is true");
                if (options.FindBufferLowerCase.Length != options.FindBufferUpperCase.Length) throw new ArgumentException("FindBufferUpperCase and FindBufferUpperCase must have the same size when Type is Text and MatchCase is true");
                buffer1 = options.FindBufferLowerCase;
                buffer2 = options.FindBufferUpperCase;

            }
            else if (options.Type == FindType.Hex)
            {
                if (options.Hex == null || options.Hex.Length == 0) throw new ArgumentException("Hex can not be null when Type is Hex");
                buffer1 = options.Hex;
            }

            int buffer1Length = buffer1.Length;

            _abortFind = false;

            bool forward = options.FindDirection == Direction.Forward;
            startIndex = startIndex + SelectionLength * (forward ? 1 : -1);
            for (long pos = startIndex; (forward ? pos < _byteProvider.Length : pos >= 0); pos += (forward ? 1 : -1))
            {
                if (_abortFind) return -2;

                if (pos % 1000 == 0) Application.DoEvents(); // for performance reasons: DoEvents only 1 times per 1000 loops

                byte compareByte = _byteProvider.ReadByte(pos);
                bool buffer1Match = compareByte == buffer1[(forward ? 0 : buffer1.Length - 1) + match * (forward ? 1 : -1)];
                bool hasBuffer2 = buffer2 != null;
                bool buffer2Match = hasBuffer2 ? compareByte == buffer2[(forward ? 0 : buffer2.Length - 1) + match * (forward ? 1 : -1)] : false;
                bool isMatch = buffer1Match || buffer2Match;
                if (!isMatch)
                {
                    pos -= match * (forward ? 1 : -1);
                    match = 0;
                    CurrentFindingPosition = pos;
                    continue;
                }

                match++;

                if (match == buffer1Length)
                {
                    long bytePos = pos - (forward ? buffer1Length - 1 : 0);
                    Select(bytePos, buffer1Length);
                    ScrollByteIntoView(_bytePos + _selectionLength);
                    ScrollByteIntoView(_bytePos);

                    return bytePos;
                }
            }

            return -1;
        }

        /// <summary>
        /// Aborts a working Find method.
        /// </summary>
        public void AbortFind() => _abortFind = true;
        #endregion

        #region Copy, Cut and Paste methods
        byte[] GetCopyData()
        {
            if (!CanCopy()) return new byte[0];

            // put bytes into buffer
            byte[] buffer = new byte[_selectionLength];
            int id = -1;
            for (long i = _bytePos; i < _bytePos + _selectionLength; i++)
            {
                id++;

                buffer[id] = _byteProvider.ReadByte(i);
            }
            return buffer;
        }

        /// <summary>
        /// Copies the current selection in the hex box to the Clipboard.
        /// </summary>
        /// <param name="dataHexStr">Determines whether the copied content-type is a Hex string</param>
        public void Copy(bool dataHexStr = false)
        {
            if (!CanCopy()) return;

            // put bytes into buffer
            byte[] buffer = GetCopyData();

            DataObject da = new DataObject();

            // set string buffer clipbard data
            string sBuffer = dataHexStr ? ConvertBytesToHex(buffer) : ByteCharConverter.ToString(buffer);
            da.SetData(typeof(string), sBuffer);

            //set memorystream (BinaryData) clipboard data
            System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer, 0, buffer.Length, false, true);
            da.SetData("BinaryData", ms);

            Clipboard.SetDataObject(da, true);
            UpdateCaret();
            ScrollByteIntoView();
            Invalidate();

            OnCopied(EventArgs.Empty);
        }

        /// <summary>
        /// Return true if Copy method could be invoked.
        /// </summary>
        public bool CanCopy()
        {
            if (_selectionLength < 1 || _byteProvider == null) return false;

            return true;
        }

        /// <summary>
        /// Moves the current selection in the hex box to the Clipboard.
        /// </summary>
        public void Cut()
        {
            if (!CanCut()) return;

            Copy();

            _byteProvider.DeleteBytes(_bytePos, _selectionLength);
            _byteCharacterPos = 0;
            UpdateCaret();
            ScrollByteIntoView();
            ReleaseSelection();
            Invalidate();
            Refresh();
        }

        /// <summary>
        /// Return true if Cut method could be invoked.
        /// </summary>
        public bool CanCut()
        {
            if (ReadOnly || !EnableCut || !Enabled) return false;

            if (_byteProvider == null) return false;

            if (_selectionLength < 1 || !_byteProvider.SupportsDeleteBytes()) return false;

            return true;
        }

        /// <summary>
        /// Replaces the current selection in the hex box with the contents of the Clipboard.
        /// </summary>
        /// <param name="dataHexStr">Determines whether the copied content-type is a Hex string</param>
        public void Paste(bool dataHexStr = false)
        {
            if (!CanPaste()) return;

            byte[] buffer = null;
            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent("BinaryData"))
            {
                System.IO.MemoryStream ms = (System.IO.MemoryStream)da.GetData("BinaryData");
                buffer = new byte[ms.Length];
                ms.Read(buffer, 0, buffer.Length);
            }
            else if (da.GetDataPresent(typeof(string)))
            {
                string sBuffer = (string)da.GetData(typeof(string));
                if (dataHexStr)
                {
                    sBuffer = sBuffer.Replace(" ", "").Replace("-", "").Replace("_", "");
                    if (sBuffer.Length % 2 == 1) sBuffer = "0" + sBuffer;
                    buffer = new byte[sBuffer.Length / 2];
                    for (int idx = 0; idx < buffer.Length; idx++) buffer[idx] = Convert.ToByte(sBuffer.Substring(idx * 2, 2), 16);
                }
                else buffer = System.Text.Encoding.ASCII.GetBytes(sBuffer);
            }
            else return;

            if (EnableOverwritePaste) _selectionLength = buffer.Length;
            if (_selectionLength > 0) _byteProvider.DeleteBytes(_bytePos, _selectionLength);

            _byteProvider.InsertBytes(_bytePos, buffer);
            for (long pos = _bytePos; pos < _bytePos + buffer.Length; pos++) if (!changedPosSet.Contains(pos)) changedPosSet.Add(pos);
            SetPosition(_bytePos + buffer.Length, 0);

            ReleaseSelection();
            ScrollByteIntoView();
            UpdateCaret();
            Invalidate();
        }

        /// <summary>
        /// Return true if Paste method could be invoked.
        /// </summary>
        public bool CanPaste()
        {
            if (ReadOnly || !EnablePaste || !Enabled) return false;

            if (_byteProvider == null || !_byteProvider.SupportsInsertBytes()) return false;

            if (!_byteProvider.SupportsDeleteBytes() && _selectionLength > 0) return false;

            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent("BinaryData")) return true;
            else if (da.GetDataPresent(typeof(string))) return true;
            else return false;
        }

        /// <summary>
        /// Return true if PasteHex method could be invoked.
        /// </summary>
        public bool CanPasteHex()
        {
            if (!CanPaste()) return false;

            byte[] buffer = null;
            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent(typeof(string)))
            {
                string hexString = (string)da.GetData(typeof(string));
                buffer = ConvertHexToBytes(hexString);
                return (buffer != null);
            }
            return false;
        }
        #endregion

        #region Paint methods
        /// <summary>
        /// Paints the background.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            switch (_borderStyle)
            {
                case BorderStyle.Fixed3D:
                    if (TextBoxRenderer.IsSupported)
                    {
                        VisualStyleElement state = VisualStyleElement.TextBox.TextEdit.Normal;
                        Color backColor = BackColor;

                        if (Enabled)
                        {
                            if (ReadOnly) state = VisualStyleElement.TextBox.TextEdit.ReadOnly;
                            else if (Focused) state = VisualStyleElement.TextBox.TextEdit.Focused;
                        }
                        else
                        {
                            state = VisualStyleElement.TextBox.TextEdit.Disabled;
                            backColor = BackColorDisabled;
                        }

                        VisualStyleRenderer vsr = new VisualStyleRenderer(state);
                        vsr.DrawBackground(e.Graphics, ClientRectangle);

                        Rectangle rectContent = vsr.GetBackgroundContentRectangle(e.Graphics, ClientRectangle);
                        e.Graphics.FillRectangle(new SolidBrush(backColor), rectContent);
                    }
                    else
                    {
                        // draw background
                        e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

                        // draw default border
                        ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken);
                    }

                    break;
                case BorderStyle.FixedSingle:
                    // draw background
                    e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

                    // draw fixed single border
                    ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Black, ButtonBorderStyle.Solid);
                    break;
                default:
                    // draw background
                    e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);
                    break;
            }
        }


        /// <summary>
        /// Paints the hex box.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_byteProvider == null) return;

            System.Diagnostics.Debug.WriteLine("OnPaint " + DateTime.Now.ToString(), "HexBox");

            // draw only in the content rectangle, so exclude the border and the scrollbar.
            Region r = new Region(ClientRectangle);
            r.Exclude(_recContent);
            e.Graphics.ExcludeClip(r);

            UpdateVisibilityBytes();

            if (_lineInfoVisible) PaintLineInfo(e.Graphics, _startByte, _endByte);

            if (!_stringViewVisible) PaintHex(e.Graphics, _startByte, _endByte);
            else
            {
                PaintHexAndStringView(e.Graphics, _startByte, _endByte);
                if (_shadowSelectionVisible) PaintCurrentBytesSign(e.Graphics);
            }
            if (_columnInfoVisible) PaintHeaderRow(e.Graphics);
            if (_groupSeparatorVisible) PaintColumnSeparator(e.Graphics);
        }


        void PaintLineInfo(Graphics g, long startByte, long endByte)
        {
            // Ensure endByte isn't > length of array.
            endByte = Math.Min(_byteProvider.Length - 1, endByte);

            Color lineInfoColor = (InfoForeColor != Color.Empty) ? InfoForeColor : ForeColor;
            using (Brush brush = new SolidBrush(lineInfoColor))
            {
                int maxLine = GetGridBytePoint(endByte - startByte).Y + 1;
                for (int i = 0; i < maxLine; i++)
                {
                    long firstLineByte = (startByte + (_iHexMaxHBytes) * i) + _lineInfoOffset;

                    PointF bytePointF = GetBytePointF(new Point(0, 0 + i));
                    string info = firstLineByte.ToString(_hexStringFormat, System.Threading.Thread.CurrentThread.CurrentCulture);
                    if (info.Length > LineInfoOffsetLength) info = info.Substring(info.Length - LineInfoOffsetLength);

                    int nulls = LineInfoOffsetLength - info.Length;
                    string formattedInfo;
                    if (nulls > -1) formattedInfo = new string('0', LineInfoOffsetLength - info.Length) + info;
                    else formattedInfo = new string('~', LineInfoOffsetLength);

                    g.DrawString(formattedInfo, Font, brush, new PointF(_recLineInfo.X, bytePointF.Y), _stringFormat);
                }
            }
        }

        void PaintHeaderRow(Graphics g)
        {
            using (Brush brush = new SolidBrush(InfoForeColor))
                for (int col = 0; col < _iHexMaxHBytes; col++) PaintColumnInfo(g, new byte[] { (byte)col }, brush, col);
        }

        void PaintColumnSeparator(Graphics g)
        {
            for (int col = GroupSize; col < _iHexMaxHBytes; col += GroupSize)
            {
                var pen = new Pen(new SolidBrush(InfoForeColor), 1);
                PointF headerPointF = GetColumnInfoPointF(col);
                headerPointF.X -= CharSize.Width / 4;
                g.DrawLine(pen, headerPointF, new PointF(headerPointF.X, headerPointF.Y + _recColumnInfo.Height + _recHex.Height));
                if (!StringViewVisible) continue;

                PointF byteStringPointF = GetByteStringPointF(new Point(col, 0));
                headerPointF.X -= 2;
                g.DrawLine(pen, new PointF(byteStringPointF.X, byteStringPointF.Y), new PointF(byteStringPointF.X, byteStringPointF.Y + _recHex.Height));
            }
        }

        void PaintHex(Graphics g, long startByte, long endByte)
        {
            Brush brush = new SolidBrush(GetDefaultForeColor());
            Brush zeroBrush = new SolidBrush(ZeroBytesForeColor);
            Brush changedBrush = new SolidBrush(ChangedForeColor);
            Brush changedFinishBrush = new SolidBrush(ChangedFinishForeColor);
            Brush selBrush = new SolidBrush(SelectionForeColor);
            Brush selBrushBack = new SolidBrush(SelectionBackColor);

            int counter = -1;
            long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

            bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);

            for (long i = startByte; i < intern_endByte + 1; i++)
            {
                counter++;
                Point gridPoint = GetGridBytePoint(counter);
                byte[] data = _byteProvider.ReadBytes(i, ByteGroupingSize);

                bool isSelectedByte = i >= _bytePos && i <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

                if (isSelectedByte && isKeyInterpreterActive) PaintHexStringSelected(g, data, selBrush, selBrushBack, gridPoint);
                else if (changedPosSet.Contains(i)) PaintHexString(g, data, changedBrush, gridPoint);
                else if (changedFinishPosSet.Contains(i)) PaintHexString(g, data, changedFinishBrush, gridPoint);
                else if (CheckEmptyData(data)) PaintHexString(g, data, zeroBrush, gridPoint);
                else PaintHexString(g, data, brush, gridPoint);
                if (ByteGroupingSize != 1)
                {
                    for (int idx = 1; idx < ByteGroupingSize; idx++)
                    {
                        i++;
                        counter++;
                        if (changedPosSet.Contains(i)) PaintHexString(g, data, changedBrush, gridPoint);
                        else if (changedFinishPosSet.Contains(i)) PaintHexString(g, data, changedFinishBrush, gridPoint);
                    }
                }
            }
        }

        void PaintHexString(Graphics g, byte[] data, Brush brush, Point gridPoint)
        {
            PointF bytePointF = GetBytePointF(gridPoint);

            string sB;
            if (ByteGroupingSize > 1 && data.Length < ByteGroupingSize) Array.Resize(ref data, ByteGroupingSize);

            if (ByteGrouping == ByteGroupingType.B01Decimal) sB = data[0].ToString();
            else if (ByteGrouping == ByteGroupingType.B02Decimal) sB = BitConverter.ToUInt16(data, 0).ToString();
            else if (ByteGrouping == ByteGroupingType.B04Decimal) sB = BitConverter.ToUInt32(data, 0).ToString();
            else if (ByteGrouping == ByteGroupingType.B08Decimal) sB = BitConverter.ToUInt64(data, 0).ToString();
            else if (ByteGrouping == ByteGroupingType.B04Float) sB = FormatFloating(BitConverter.ToSingle(data, 0));
            else if (ByteGrouping == ByteGroupingType.B08Double) sB = FormatFloating(BitConverter.ToDouble(data, 0));
            else sB = ConvertBytesToHex(data);

            for (int idx = 0; idx < sB.Length; idx++)
            {
                g.DrawString(sB[idx].ToString(), Font, brush, bytePointF, _stringFormat);
                if (idx < sB.Length -1) bytePointF.X += CharSize.Width;
            }
        }

        string FormatFloating(object number)
        {
            string result = "";

            if (number is float numF)
            {
                if (numF == 0) result = "0";
                else if(numF > -1 && numF < 1) result = String.Format("{0:0.###}", numF);
                else if(numF > int.MaxValue || numF < int.MinValue) result = String.Format("{0:E4}", numF);
                else result = String.Format("{0:0.###}", numF);
            }
            else if (number is double numD)
            {
                if (numD == 0) result = "0";
                else if (numD > -1 && numD < 1) result = String.Format("{0:0.#####}", numD);
                else if (numD > long.MaxValue || numD < long.MinValue) result = String.Format("{0:E8}", numD);
                else result = String.Format("{0:0.#####}", numD);
            }

            return result;
        }

        void PaintColumnInfo(Graphics g, byte[] data, Brush brush, int col)
        {
            PointF headerPointF = GetColumnInfoPointF(col);

            string sB = ConvertBytesToHex(data);
            for (int idx = 0; idx < sB.Length; idx++)
            {
                g.DrawString(sB[idx].ToString(), Font, brush, headerPointF, _stringFormat);
                if (idx < sB.Length - 1) headerPointF.X += CharSize.Width;
            }
        }

        void PaintHexStringSelected(Graphics g, byte[] data, Brush brush, Brush brushBack, Point gridPoint)
        {
            string sB = ConvertBytesToHex(data);

            PointF bytePointF = GetBytePointF(gridPoint);

            bool isLastLineChar = (gridPoint.X + 1 == _iHexMaxHBytes);
            float bcWidth = (isLastLineChar || ByteGroupingSize > 1) ? CharSize.Width * 2 : CharSize.Width * 3;

            g.FillRectangle(brushBack, bytePointF.X, bytePointF.Y, bcWidth * data.Length, CharSize.Height);
            for (int idx = 0; idx < sB.Length; idx++)
            {
                g.DrawString(sB[idx].ToString(), Font, brush, bytePointF, _stringFormat);
                if (idx < sB.Length - 1) bytePointF.X += CharSize.Width;
            }
        }

        void PaintHexAndStringView(Graphics g, long startByte, long endByte)
        {
            using (Brush brush = new SolidBrush(GetDefaultForeColor()))
            using (Brush zeroBrush = new SolidBrush(ZeroBytesForeColor))
            using (Brush changedBrush = new SolidBrush(ChangedForeColor))
            using (Brush changedFinishBrush = new SolidBrush(ChangedFinishForeColor))
            using (Brush selBrush = new SolidBrush(SelectionForeColor))
            using (Brush selBrushBack = new SolidBrush(SelectionBackColor))
            {
                int counter = -1;
                long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

                bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);
                bool isStringKeyInterpreterActive = _keyInterpreter != null && _keyInterpreter.GetType() == typeof(StringKeyInterpreter);

                string str = "", strTmp = "";
                int strBuffLen = 20;
                var defaultConvert = ByteCharConverter.getEncoding() == null;
                for (long idx = startByte; idx < intern_endByte + 1; idx++)
                {
                    counter++;
                    Point gridPoint = GetGridBytePoint(counter);
                    PointF byteStringPointF = GetByteStringPointF(gridPoint);
                    var data = _byteProvider.ReadBytes(idx, ByteGroupingSize);

                    bool isSelectedByte = idx >= _bytePos && idx <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

                    if (isSelectedByte && isKeyInterpreterActive) PaintHexStringSelected(g, data, selBrush, selBrushBack, gridPoint);
                    else if (changedPosSet.Contains(idx)) PaintHexString(g, data, changedBrush, gridPoint);
                    else if (changedFinishPosSet.Contains(idx)) PaintHexString(g, data, changedFinishBrush, gridPoint);
                    else if (CheckEmptyData(data)) PaintHexString(g, data, zeroBrush, gridPoint);
                    else PaintHexString(g, data, brush, gridPoint);

                    int currentIdx = (int)((idx - startByte) % strBuffLen);
                    if (defaultConvert) str = ByteCharConverter.ToString(data);
                    else if (currentIdx == 0)
                    {
                        if (str.Length > strBuffLen)
                        {
                            strTmp = str.Substring(strBuffLen);
                            if (strTmp.Length > 3) strTmp = strTmp.Substring(0, strTmp.Length - 3);
                        }
                        str = ByteCharConverter.ToString(_byteProvider.ReadBytes((int)idx, strBuffLen + 10), true);
                        if (str.Length > strTmp.Length && strTmp.Length > 0 && !str.StartsWith(strTmp)) str = strTmp + str.Substring(strTmp.Length);
                    }

                    if (isSelectedByte && isStringKeyInterpreterActive)
                        g.FillRectangle(selBrushBack, byteStringPointF.X, byteStringPointF.Y, CharSize.Width * data.Length, CharSize.Height);

                    if (str != "") g.DrawString(defaultConvert ? str : str[currentIdx].ToString(), Font,
                        isSelectedByte && isStringKeyInterpreterActive ? selBrush : brush, byteStringPointF, _stringFormat);

                    if (ByteGroupingSize > 1)
                    {
                        for (int idx2 = 1; idx2 < ByteGroupingSize; idx2++)
                        {
                            idx++;
                            counter++;
                            if (!defaultConvert && str != "")
                            {
                                currentIdx = (int)((idx - startByte) % strBuffLen);
                                if (currentIdx == 0)
                                {
                                    if (str.Length > strBuffLen)
                                    {
                                        strTmp = str.Substring(strBuffLen);
                                        if (strTmp.Length > 3) strTmp = strTmp.Substring(0, strTmp.Length - 3);
                                    }
                                    str = ByteCharConverter.ToString(_byteProvider.ReadBytes((int)idx, strBuffLen + 10), true);
                                    if (str.Length > strTmp.Length && strTmp.Length > 0 && !str.StartsWith(strTmp)) str = strTmp + str.Substring(strTmp.Length);
                                }
                                if (currentIdx < str.Length)
                                {
                                    gridPoint = GetGridBytePoint(counter);
                                    byteStringPointF = GetByteStringPointF(gridPoint);
                                    g.DrawString(str[currentIdx].ToString(), Font, isSelectedByte && isStringKeyInterpreterActive ? selBrush : brush, byteStringPointF, _stringFormat);
                                }
                            }
                            if (changedPosSet.Contains(idx)) PaintHexString(g, data, changedBrush, gridPoint);
                            else if (changedFinishPosSet.Contains(idx)) PaintHexString(g, data, changedFinishBrush, gridPoint);
                        }
                    }
                }
            }
        }

        bool CheckEmptyData(byte[] data)
        {
            for (int i = 0; i < data.Length; i++) if (data[i] != 0) return false;
            return true;
        }

        void PaintCurrentBytesSign(Graphics g)
        {
            if (_keyInterpreter == null || _bytePos == -1 || !Enabled) return;
            if (_keyInterpreter.GetType() == typeof(KeyInterpreter))
            {
                if (_selectionLength == 0)
                {
                    Point gp = GetGridBytePoint(_bytePos - _startByte);
                    PointF pf = GetByteStringPointF(gp);
                    Size s = new Size((int)CharSize.Width, (int)CharSize.Height);
                    Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
                    if (r.IntersectsWith(_recStringView))
                    {
                        r.Intersect(_recStringView);
                        PaintCurrentByteSign(g, r);
                    }
                }
                else
                {
                    int lineWidth = (int)(_recStringView.Width - CharSize.Width);

                    Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
                    PointF startSelPointF = GetByteStringPointF(startSelGridPoint);

                    Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
                    PointF endSelPointF = GetByteStringPointF(endSelGridPoint);

                    int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
                    if (multiLine == 0)
                    {
                        Rectangle singleLine = new Rectangle(
                            (int)startSelPointF.X,
                            (int)startSelPointF.Y,
                            (int)(endSelPointF.X - startSelPointF.X + CharSize.Width),
                            (int)CharSize.Height);
                        if (singleLine.IntersectsWith(_recStringView))
                        {
                            singleLine.Intersect(_recStringView);
                            PaintCurrentByteSign(g, singleLine);
                        }
                    }
                    else
                    {
                        Rectangle firstLine = new Rectangle(
                            (int)startSelPointF.X,
                            (int)startSelPointF.Y,
                            (int)(_recStringView.X + lineWidth - startSelPointF.X + CharSize.Width),
                            (int)CharSize.Height);
                        if (firstLine.IntersectsWith(_recStringView))
                        {
                            firstLine.Intersect(_recStringView);
                            PaintCurrentByteSign(g, firstLine);
                        }

                        if (multiLine > 1)
                        {
                            Rectangle betweenLines = new Rectangle(
                                _recStringView.X,
                                (int)(startSelPointF.Y + CharSize.Height),
                                (int)(_recStringView.Width),
                                (int)(CharSize.Height * (multiLine - 1)));
                            if (betweenLines.IntersectsWith(_recStringView))
                            {
                                betweenLines.Intersect(_recStringView);
                                PaintCurrentByteSign(g, betweenLines);
                            }

                        }

                        Rectangle lastLine = new Rectangle(
                            _recStringView.X,
                            (int)endSelPointF.Y,
                            (int)(endSelPointF.X - _recStringView.X + CharSize.Width),
                            (int)CharSize.Height);
                        if (lastLine.IntersectsWith(_recStringView))
                        {
                            lastLine.Intersect(_recStringView);
                            PaintCurrentByteSign(g, lastLine);
                        }
                    }
                }
            }
            else
            {
                if (_selectionLength == 0)
                {
                    Point gp = GetGridBytePoint(_bytePos - _startByte);
                    PointF pf = GetBytePointF(gp);
                    Size s = new Size((int)CharSize.Width * 2, (int)CharSize.Height);
                    Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
                    PaintCurrentByteSign(g, r);
                }
                else
                {
                    int lineWidth = (int)(_recHex.Width - CharSize.Width * 5);

                    Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
                    PointF startSelPointF = GetBytePointF(startSelGridPoint);

                    Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
                    PointF endSelPointF = GetBytePointF(endSelGridPoint);

                    int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
                    if (multiLine == 0)
                    {
                        Rectangle singleLine = new Rectangle(
                            (int)startSelPointF.X,
                            (int)startSelPointF.Y,
                            (int)(endSelPointF.X - startSelPointF.X + CharSize.Width * 2),
                            (int)CharSize.Height);
                        if (singleLine.IntersectsWith(_recHex))
                        {
                            singleLine.Intersect(_recHex);
                            PaintCurrentByteSign(g, singleLine);
                        }
                    }
                    else
                    {
                        Rectangle firstLine = new Rectangle(
                            (int)startSelPointF.X,
                            (int)startSelPointF.Y,
                            (int)(_recHex.X + lineWidth - startSelPointF.X + CharSize.Width * 2),
                            (int)CharSize.Height);
                        if (firstLine.IntersectsWith(_recHex))
                        {
                            firstLine.Intersect(_recHex);
                            PaintCurrentByteSign(g, firstLine);
                        }

                        if (multiLine > 1)
                        {
                            Rectangle betweenLines = new Rectangle(
                                _recHex.X,
                                (int)(startSelPointF.Y + CharSize.Height),
                                (int)(lineWidth + CharSize.Width * 2),
                                (int)(CharSize.Height * (multiLine - 1)));
                            if (betweenLines.IntersectsWith(_recHex))
                            {
                                betweenLines.Intersect(_recHex);
                                PaintCurrentByteSign(g, betweenLines);
                            }

                        }

                        Rectangle lastLine = new Rectangle(
                            _recHex.X,
                            (int)endSelPointF.Y,
                            (int)(endSelPointF.X - _recHex.X + CharSize.Width * 2),
                            (int)CharSize.Height);
                        if (lastLine.IntersectsWith(_recHex))
                        {
                            lastLine.Intersect(_recHex);
                            PaintCurrentByteSign(g, lastLine);
                        }
                    }
                }
            }
        }

        void PaintCurrentByteSign(Graphics g, Rectangle rec)
        {
            // stack overflowexception on big files - workaround
            if (rec.Top < 0 || rec.Left < 0 || rec.Width <= 0 || rec.Height <= 0) return;

            using (Bitmap myBitmap = new Bitmap(rec.Width, rec.Height))
            using (Graphics bitmapGraphics = Graphics.FromImage(myBitmap))
            using (SolidBrush greenBrush = new SolidBrush(_shadowSelectionColor))
            {
                bitmapGraphics.FillRectangle(greenBrush, 0, 0, rec.Width, rec.Height);
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
                g.DrawImage(myBitmap, rec.Left, rec.Top);
            }
        }

        Color GetDefaultForeColor() { return Enabled ? ForeColor : Color.Gray; }
        void UpdateVisibilityBytes()
        {
            if (_byteProvider == null || _byteProvider.Length == 0) return;

            _startByte = (ScrollVpos + 1) * _iHexMaxHBytes - _iHexMaxHBytes;
            _endByte = (long)Math.Min(_byteProvider.Length - 1, _startByte + _iHexMaxBytes);
        }
        #endregion

        #region Positioning methods
        void UpdateRectanglePositioning()
        {
            // calc char size
            SizeF charSize;
            using (var graphics = CreateGraphics())
            {
                charSize = graphics.MeasureString("A", Font, 100, _stringFormat);
            }
            CharSize = new SizeF((float)Math.Ceiling(charSize.Width), (float)Math.Ceiling(charSize.Height));

            int requiredWidth = 0;
            // calc content bounds
            _recContent = ClientRectangle;
            _recContent.X += _recBorderLeft;
            _recContent.Y += _recBorderTop;
            _recContent.Width -= _recBorderRight + _recBorderLeft;
            _recContent.Height -= _recBorderBottom + _recBorderTop;

            if (_vScrollBarVisible)
            {
                _recContent.Width -= _vScrollBar.Width;
                _vScrollBar.Left = _recContent.X + _recContent.Width;
                _vScrollBar.Top = _recContent.Y;
                _vScrollBar.Height = _recContent.Height;
                requiredWidth += _vScrollBar.Width;
            }

            int marginLeft = 4;
            // calc line info bounds
            if (_lineInfoVisible)
            {
                _recLineInfo = new Rectangle(_recContent.X + marginLeft, _recContent.Y, (int)(CharSize.Width * LineInfoOffsetLength), _recContent.Height);
                requiredWidth += _recLineInfo.Width;
            }
            else
            {
                _recLineInfo = Rectangle.Empty;
                _recLineInfo.X = marginLeft;
                requiredWidth += marginLeft;
            }

            // calc line info bounds
            _recColumnInfo = new Rectangle(_recLineInfo.X + _recLineInfo.Width, _recContent.Y, _recContent.Width - _recLineInfo.Width, (int)charSize.Height + 4);
            if (_columnInfoVisible)
            {
                _recLineInfo.Y += (int)charSize.Height + 4;
                _recLineInfo.Height -= (int)charSize.Height + 4;
            }
            else _recColumnInfo.Height = 0;

            // calc hex bounds and grid
            _recHex = new Rectangle(_recLineInfo.X + _recLineInfo.Width, _recLineInfo.Y, _recContent.Width - _recLineInfo.Width, _recContent.Height - _recColumnInfo.Height);
            if (UseFixedBytesPerLine)
            {
                SetHorizontalByteCount(_bytesPerLine);
                _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * CharSize.Width * 3 + (2 * CharSize.Width));
                requiredWidth += _recHex.Width;
            }
            else
            {
                int hmax = (int)Math.Floor((double)_recHex.Width / (double)CharSize.Width);
                if (_stringViewVisible)
                {
                    hmax -= 2;
                    if (hmax > 1) SetHorizontalByteCount((int)Math.Floor((double)hmax / 4));
                    else SetHorizontalByteCount(1);
                }
                else
                {
                    if (hmax > 1) SetHorizontalByteCount((int)Math.Floor((double)hmax / 3));
                    else SetHorizontalByteCount(1);
                }
                _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * CharSize.Width * 3 + (2 * CharSize.Width));
                requiredWidth += _recHex.Width;
            }

            if (_stringViewVisible)
            {
                _recStringView = new Rectangle(_recHex.X + _recHex.Width,  _recHex.Y, (int)(CharSize.Width * _iHexMaxHBytes), _recHex.Height);
                requiredWidth += _recStringView.Width;
            }
            else _recStringView = Rectangle.Empty;

            RequiredWidth = requiredWidth;

            int vmax = (int)Math.Floor((double)_recHex.Height / (double)CharSize.Height);
            SetVerticalByteCount(vmax);

            _iHexMaxBytes = _iHexMaxHBytes * _iHexMaxVBytes;

            UpdateScrollSize();
        }

        PointF GetBytePointF(long byteIndex)
        {
            Point gp = GetGridBytePoint(byteIndex);
            return GetBytePointF(gp);
        }

        PointF GetBytePointF(Point gp)
        {
            float x = (ByteGroupingSize == 1 ? 3 : 2) * CharSize.Width * gp.X + _recHex.X;
            float y = (gp.Y + 1) * CharSize.Height - CharSize.Height + _recHex.Y;

            if (ByteGroupingSize != 1) x += ByteGroupingSize * CharSize.Width * (gp.X / ByteGroupingSize);

            return new PointF(x, y);
        }

        PointF GetColumnInfoPointF(int col)
        {
            Point gp = GetGridBytePoint(col);
            float x = (ByteGroupingSize == 1 ? 3 : 2) * CharSize.Width * gp.X + _recColumnInfo.X;
            float y = _recColumnInfo.Y;

            if (ByteGroupingSize != 1) x += ByteGroupingSize * CharSize.Width * (gp.X / ByteGroupingSize);

            return new PointF(x, y);
        }

        PointF GetByteStringPointF(Point gp)
        {
            float x = (CharSize.Width) * gp.X + _recStringView.X;
            float y = (gp.Y + 1) * CharSize.Height - CharSize.Height + _recStringView.Y;

            return new PointF(x, y);
        }

        Point GetGridBytePoint(long byteIndex)
        {
            int row = (int)Math.Floor((double)byteIndex / (double)_iHexMaxHBytes);
            int column = (int)(byteIndex + _iHexMaxHBytes - _iHexMaxHBytes * (row + 1));

            Point res = new Point(column, row);
            return res;
        }

        /// <summary>Get changed position set</summary>
        public HashSet<long> GetChangedPosSet() => changedPosSet;

        /// <summary>Get changed finish position set</summary>
        public HashSet<long> GetChangedFinishPosSet() => changedFinishPosSet;

        /// <summary>Get changed and finish position list</summary>
        public List<long> GetChangedFinishPosList()
        {
            HashSet<long> changedAndFinishPosSetSet = new HashSet<long>(changedFinishPosSet);
            changedAndFinishPosSetSet.UnionWith(changedPosSet);
            return new List<long>(changedAndFinishPosSetSet);
        }

        /// <summary>
        /// Set to finish for changed position, these position are saved when editing bytes value
        /// </summary>
        public void ChangedPosSetFinish()
        {
            if (changedPosSet == null || changedPosSet.Count == 0) return;
            if (changedFinishPosSet == null) changedFinishPosSet = new HashSet<long>();

            changedFinishPosSet.UnionWith(changedPosSet);
            changedPosSet.Clear();
        }

        /// <summary>
        /// Clear the position of changed
        /// </summary>
        public void ChangedPosClear()
        {
            if (changedPosSet == null || changedPosSet.Count == 0) return;
            else changedPosSet.Clear();
        }

        /// <summary>
        /// Clear the position of finish changed
        /// </summary>
        public void ChangedFinishPosClear()
        {
            if (changedFinishPosSet == null || changedFinishPosSet.Count == 0) return;
            else changedFinishPosSet.Clear();
        }
        #endregion

        #region Overridden properties
        /// <summary>
        /// Gets or sets the background color for the control.
        /// </summary>
        [DefaultValue(typeof(Color), "White")]
        public override Color BackColor
        {
            get => base.BackColor;
            set => base.BackColor = value;
        }

        /// <summary>
        /// The font used to display text in the hexbox.
        /// </summary>
        public override Font Font
        {
            get => base.Font;
            set
            {
                if (value == null) return;

                base.Font = value;
                UpdateRectanglePositioning();
                Invalidate();
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
        public override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
        public override RightToLeft RightToLeft
        {
            get => base.RightToLeft;
            set => base.RightToLeft = value;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the background color for the disabled control.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the background color for the disabled control."), DefaultValue(typeof(Color), "WhiteSmoke")]
        public Color BackColorDisabled { get; set; } = Color.FromName("WhiteSmoke");

        /// <summary>
        /// Get or set whether to make the HexEditor readonly.
        /// </summary>
        /// <remarks>
        /// Disable edit, cut, paste, delete behavior when set to true.
        /// </remarks>
        [Category("HexBehavior"), Description("Get or set whether to make the HexEditor readonly."), DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                if (_readOnly == value) return;

                _readOnly = value;
                OnReadOnlyChanged(EventArgs.Empty);
                Invalidate();
            }
        }
        bool _readOnly;

        /// <summary>
        /// Gets or sets the maximum count of bytes in one line.
        /// </summary>
        /// <remarks>
        /// UseFixedBytesPerLine property no longer has to be set to true for this to work
        /// </remarks>
        [Category("HexBehavior"), Description("Gets or sets the maximum count of bytes in one line."), DefaultValue(16)]
        public int BytesPerLine
        {
            get => _bytesPerLine;
            set
            {
                if (_bytesPerLine == value) return;

                _bytesPerLine = value;
                OnBytesPerLineChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        int _bytesPerLine = 16;

        /// <summary>
        /// Gets or sets the number of bytes in a group. Used to show the group separator line (if GroupSeparatorVisible is true)
        /// </summary>
        /// <remarks>
        /// GroupSeparatorVisible property must set to true
        /// </remarks>
        [Category("HexBehavior"), Description("Gets or sets the byte-count between group separators (if visible)."), DefaultValue(4)]
        public int GroupSize
        {
            get => _groupSize;
            set
            {
                if (_groupSize == value) return;

                _groupSize = value;
                OnGroupSizeChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        int _groupSize = 4;

        /// <summary>
        /// Gets or sets if the count of bytes in one line is fix.
        /// </summary>
        /// <remarks>
        /// When set to True, BytesPerLine property determine the maximum count of bytes in one line.
        /// </remarks>
        [Category("HexBehavior"), Description("Gets or sets if the count of bytes in one line is fix."), DefaultValue(false)]
        public bool UseFixedBytesPerLine
        {
            get => _useFixedBytesPerLine;
            set
            {
                if (_useFixedBytesPerLine == value) return;

                _useFixedBytesPerLine = value;
                OnUseFixedBytesPerLineChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _useFixedBytesPerLine;

        /// <summary>
        /// Gets or sets the visibility of a vertical scroll bar.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of a vertical scroll bar."), DefaultValue(false)]
        public bool VScrollBarVisible
        {
            get => _vScrollBarVisible;
            set
            {
                if (_vScrollBarVisible == value) return;

                _vScrollBarVisible = value;

                if (_vScrollBarVisible) Controls.Add(_vScrollBar);
                else Controls.Remove(_vScrollBar);

                UpdateRectanglePositioning();
                UpdateScrollSize();

                OnVScrollBarVisibleChanged(EventArgs.Empty);
            }
        }
        bool _vScrollBarVisible;

        /// <summary>
        /// Gets or sets the visibility of the group separator.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of a separator vertical line."), DefaultValue(false)]
        public bool GroupSeparatorVisible
        {
            get => _groupSeparatorVisible;
            set
            {
                if (_groupSeparatorVisible == value) return;

                _groupSeparatorVisible = value;
                OnGroupSeparatorVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _groupSeparatorVisible = false;

        /// <summary>
        /// Gets or sets the visibility of the column info
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of header row."), DefaultValue(false)]
        public bool ColumnInfoVisible
        {
            get => _columnInfoVisible;
            set
            {
                if (_columnInfoVisible == value) return;

                _columnInfoVisible = value;
                OnColumnInfoVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _columnInfoVisible = false;

        /// <summary>
        /// Gets or sets the visibility of a line info.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of a line info."), DefaultValue(false)]
        public bool LineInfoVisible
        {
            get => _lineInfoVisible;
            set
            {
                if (_lineInfoVisible == value) return;

                _lineInfoVisible = value;
                OnLineInfoVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _lineInfoVisible = false;

        /// <summary>
        /// Gets or sets the offset of a line info.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the offset of the line info."), DefaultValue((long)0)]
        public long LineInfoOffset
        {
            get => _lineInfoOffset;
            set
            {
                if (_lineInfoOffset == value) return;

                _lineInfoOffset = value;

                Invalidate();
            }
        }
        long _lineInfoOffset = 0;

        /// <summary>
        /// Gets or sets the hex box's border style.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the hex box's border style."), DefaultValue(typeof(BorderStyle), "Fixed3D")]
        public BorderStyle BorderStyle
        {
            get => _borderStyle;
            set
            {
                if (_borderStyle == value) return;

                _borderStyle = value;
                switch (_borderStyle)
                {
                    case BorderStyle.None:
                        _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 0;
                        break;
                    case BorderStyle.Fixed3D:
                        _recBorderLeft = _recBorderRight = SystemInformation.Border3DSize.Width;
                        _recBorderTop = _recBorderBottom = SystemInformation.Border3DSize.Height;
                        break;
                    case BorderStyle.FixedSingle:
                        _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 1;
                        break;
                }

                UpdateRectanglePositioning();

                OnBorderStyleChanged(EventArgs.Empty);

            }
        }
        BorderStyle _borderStyle = BorderStyle.Fixed3D;

        /// <summary>
        /// Gets or sets the visibility of the string view.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of the string view."), DefaultValue(false)]
        public bool StringViewVisible
        {
            get => _stringViewVisible;
            set
            {
                if (_stringViewVisible == value) return;

                _stringViewVisible = value;
                OnStringViewVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _stringViewVisible;

        /// <summary>
        /// Gets or sets whether the HexBox control displays the hex characters in upper or lower case.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether the HexBox control displays the hex characters in upper or lower case."), DefaultValue(typeof(HexCasing), "Upper")]
        public HexCasing HexCasing
        {
            get => _hexStringFormat == "X" ? HexCasing.Upper : HexCasing.Lower;
            set
            {
                string format = value == HexCasing.Upper ? "X" : "x";

                if (_hexStringFormat == format) return;

                _hexStringFormat = format;
                OnHexCasingChanged(EventArgs.Empty);

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the info color used for column info and line info. When this property is null, then ForeColor property is used.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the line info color. When this property is null, then ForeColor property is used."), DefaultValue(typeof(Color), "Gray")]
        public Color InfoForeColor
        {
            get => _infoForeColor;
            set { _infoForeColor = value; Invalidate(); }
        }
        Color _infoForeColor = Color.Gray;

        /// <summary>
        /// Gets or sets the background color for the selected bytes.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the background color for the selected bytes."), DefaultValue(typeof(Color), "Blue")]
        public Color SelectionBackColor
        {
            get => _selectionBackColor;
            set { _selectionBackColor = value; Invalidate(); }
        }
        Color _selectionBackColor = Color.Blue;

        /// <summary>
        /// Gets or sets the foreground color for the selected bytes.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the foreground color for the selected bytes."), DefaultValue(typeof(Color), "White")]
        public Color SelectionForeColor
        {
            get => _selectionForeColor;
            set { _selectionForeColor = value; Invalidate(); }
        }
        Color _selectionForeColor = Color.White;

        /// <summary>
        /// Gets or sets the visibility of a shadow selection.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the visibility of a shadow selection."), DefaultValue(true)]
        public bool ShadowSelectionVisible
        {
            get => _shadowSelectionVisible;
            set
            {
                if (_shadowSelectionVisible == value) return;
                _shadowSelectionVisible = value;
                Invalidate();
            }
        }
        bool _shadowSelectionVisible = true;

        /// <summary>
        /// Gets or sets the color of the shadow selection. 
        /// </summary>
        /// <remarks>
        /// A alpha component must be given! 
        /// Default alpha = 100
        /// </remarks>
        [Category("HexAppearance"), Description("Gets or sets the color of the shadow selection.")]
        public Color ShadowSelectionColor
        {
            get => _shadowSelectionColor;
            set { _shadowSelectionColor = value; Invalidate(); }
        }
        Color _shadowSelectionColor = Color.FromArgb(100, 60, 188, 255);

        /// <summary>
        /// Gets or sets the maximum size of line info offset support length. Sets range is 8(32bit)~16(64bit).
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the maximum size of line info offset support length. Sets range is 8(32bit)~16(64bit)."), DefaultValue(8)]
        public int LineInfoOffsetLength
        {
            get => _lineInfoOffsetLength;
            set
            {
                value = value < 8 ? 8 : (value > 16 ? 16 : value);
                _lineInfoOffsetLength = value;
            }
        }
        int _lineInfoOffsetLength = 8;

        /// <summary>
        /// Gets or sets the foreground color for the position of changed bytes.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the foreground color for the position of changed bytes."), DefaultValue(typeof(Color), "Red")]
        public Color ChangedForeColor
        {
            get => _changedForeColor;
            set { _changedForeColor = value; Invalidate(); }
        }
        Color _changedForeColor = Color.Red;

        /// <summary>
        /// Gets or sets the foreground color for the position of finish changed.
        /// After perform the ChangedPosSetFinish method, the changed position setting will be finish.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the foreground color for the position of finish changed.\n" +
            "After perform the ChangedPosSetFinish method, the changed position setting will be finish."), DefaultValue(typeof(Color), "SteelBlue")]
        public Color ChangedFinishForeColor
        {
            get => _changedFinishForeColor;
            set { _changedFinishForeColor = value; Invalidate(); }
        }
        Color _changedFinishForeColor = Color.LimeGreen;

        /// <summary>
        /// Gets or sets the foreground color for the zero bytes.
        /// </summary>
        [Category("HexAppearance"), Description("Gets or sets the foreground color for the zero bytes."), DefaultValue(typeof(Color), "Silver")]
        public Color ZeroBytesForeColor
        {
            get => _zeroBytesForeColor;
            set { _zeroBytesForeColor = value; Invalidate(); }
        }
        Color _zeroBytesForeColor = Color.Silver;

        /// <summary>
        /// Gets or sets whether you can cut bytes data.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether you can cut bytes data.")]
        public bool EnableCut { get; set; } = false;

        /// <summary>
        /// Gets or sets whether you can delete bytes data.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether you can delete bytes data.")]
        public bool EnableDelete { get; set; } = false;

        /// <summary>
        /// Gets or sets whether you can paste bytes data.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether you can paste bytes data.")]
        public bool EnablePaste { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable the overwrite mode when pasting hex data.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether to enable the overwrite mode when pasting hex data.")]
        public bool EnableOverwritePaste { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to retain the position of changed when ByteProvider Changed.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether to retain the position of changed when ByteProvider changed.")]
        public bool EnableRetainChangedPos { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to retain the position of finish changed when ByteProvider Changed.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether to retain the position of finish changed when ByteProvider changed.")]
        public bool EnableRetainChangedFinishPos { get; set; } = false;

        /// <summary>
        /// Gets or sets whether auto perform the ChangedPosSetFinish method when ByteProvider changed.
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets whether auto perform the ChangedPosSetFinish method when ByteProvider changed.")]
        public bool EnableAutoChangedPosSetFinish { get; set; } = false;

        /// <summary>
        /// Gets or sets the built-in context menu.
        /// </summary>
        [Category("HexBehavior"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content), Description("Gets or sets the built-in context menu.")]
        public BuiltInContextMenu BuiltInContextMenu { get; } //Only constructor can set values

        /// <summary>
        /// Byte Grouping settable types
        /// </summary>
        public enum ByteGroupingType
        {
            /// <summary>Size:01, Hex</summary>
            B01 = 0x001,
            /// <summary>Size:02, Hex</summary>
            B02 = 0x002,
            /// <summary>Size:04, Hex</summary>
            B04 = 0x004,
            /// <summary>Size:08, Hex</summary>
            B08 = 0x008,
            /// <summary>Size:16, Hex</summary>
            B16 = 0x010,
            /// <summary>Size:01, Decimal</summary>
            B01Decimal = 0x101,
            /// <summary>Size:02, Decimal</summary>
            B02Decimal = 0x102,
            /// <summary>Size:04, Decimal</summary>
            B04Decimal = 0x104,
            /// <summary>Size:08, Decimal</summary>
            B08Decimal = 0x108,
            /// <summary>Size:04, Float</summary>
            B04Float = 0x204,
            /// <summary>Size:08, Double</summary>
            B08Double = 0x208,
        }

        /// <summary>
        /// Gets or sets the byte grouping type:
        /// DefaultValue: B01 (Size:01)<para />
        /// B01 = Size:01, Hex
        /// B02 = Size:02, Hex
        /// B04 = Size:04, Hex
        /// B08 = Size:08, Hex
        /// B16 = Size:16, Hex<para />
        /// [Experimental Features]<para />
        /// The hex value can be displayed as a numeric value, when the ByteGrouping of HexBox is set to decimal or float or double.<para />
        /// Note: This feature is for display only and does not support direct editing of values.<para />
        /// B01Decimal = Size:01, Decimal byte
        /// B02Decimal = Size:02, Decimal ushort
        /// B04Decimal = Size:04, Decimal uint
        /// B08Decimal = Size:08, Decimal ulong
        /// B04Float = Size:04, Float
        /// B08Double = Size:08, Double
        /// </summary>
        [Category("HexBehavior"), Description("Gets or sets the byte grouping type: \n" +
"DefaultValue: B01 (Size:01) \n" +
"B01 = Size:01, Hex \n" +
"B02 = Size:02, Hex \n" +
"B04 = Size:04, Hex \n" +
"B08 = Size:08, Hex \n" +
"B16 = Size:16, Hex \n" +
"[Experimental Features] \n" +
"The hex value can be displayed as a numeric value, when the ByteGrouping of HexBox is set to decimal or float or double. \n" +
"Note: This feature is for display only and does not support direct editing of values. \n" +
"B01Decimal = Size:01, Decimal byte \n" +
"B02Decimal = Size:02, Decimal ushort \n" +
"B04Decimal = Size:04, Decimal uint \n" +
"B08Decimal = Size:08, Decimal ulong \n" +
"B04Float = Size:04, Float \n" +
"B08Double = Size:08, Double\n"), DefaultValue(ByteGroupingType.B01)]
        public ByteGroupingType ByteGrouping
        {
            get => _byteGroupingType;
            set {_byteGroupingType = value;Invalidate();}
        }
        private ByteGroupingType _byteGroupingType = ByteGroupingType.B01;

        /// <summary>
        /// ByteGroupingSize
        /// </summary>
        public int ByteGroupingSize => 0xFF & (int)ByteGrouping;
        /// <summary>
        /// ByteGroupingDecimal
        /// </summary>
        public bool ByteGroupingDecimal => (0x100 & (int)ByteGrouping) == 0x100;
        /// <summary>
        /// ByteGroupingFloating
        /// </summary>
        public bool ByteGroupingFloating => (0x200 & (int)ByteGrouping) == 0x200;

        /// <summary>content-type of the copy</summary>
        public enum StringContentType
        {
            /// <summary>Char</summary>
            Char,
            /// <summary>Hex</summary>
            Hex,
        }

        /// <summary>
        /// Get or set the content-type of the copy feature for key down (Control+C), content-type is Char or Hex text.
        /// </summary>
        [Category("HexBehavior"), Description("Get or set the content-type of the copy feature for key down (Control+C), content-type is Char or Hex text")]
        public StringContentType KeyDownControlCContentType { get; set; } = StringContentType.Char;
        #endregion

        #region Visibility Hidden Properties
        /// <summary>
        /// Gets or sets the ByteProvider.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IByteProvider ByteProvider
        {
            get => _byteProvider;
            set
            {
                if (_byteProvider == value) return;

                if (value == null) ActivateEmptyKeyInterpreter();
                else ActivateKeyInterpreter();

                if (_byteProvider != null) _byteProvider.LengthChanged -= new EventHandler(_byteProvider_LengthChanged);
                _byteProvider = value;
                if (_byteProvider != null) _byteProvider.LengthChanged += new EventHandler(_byteProvider_LengthChanged);

                if (!EnableRetainChangedPos && changedPosSet != null) changedPosSet.Clear();
                if (!EnableRetainChangedFinishPos && changedFinishPosSet != null) changedFinishPosSet.Clear();
                if (EnableAutoChangedPosSetFinish) ChangedPosSetFinish();
                if (EnableRetainChangedPos && _byteProvider is DynamicByteProvider dynBP && dynBP.ChangedPosSet != null && dynBP.ChangedPosSet.Count > 0) changedPosSet = dynBP.ChangedPosSet;

                OnByteProviderChanged(EventArgs.Empty);

                if (value == null) // do not raise events if value is null
                {
                    _bytePos = -1;
                    _byteCharacterPos = 0;
                    _selectionLength = 0;

                    DestroyCaret();
                }
                else
                {
                    SetPosition(0, 0);
                    SetSelectionLength(0);

                    if (_caretVisible && Focused) UpdateCaret();
                    else CreateCaret();
                }

                CheckCurrentLineChanged();
                CheckCurrentPositionInLineChanged();

                ScrollVpos = 0;

                UpdateVisibilityBytes();
                UpdateRectanglePositioning();

                Invalidate();
            }
        }
        IByteProvider _byteProvider;

        /// <summary>
        /// Contains the scroll bars current position.
        /// </summary>
        /// 
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long ScrollVpos { get; private set; }

        /// <summary>
        /// Gets a value that indicates the current position during Find method execution.
        /// Contains a value of the current finding position.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentFindingPosition { get; private set; }

        /// <summary>
        /// Gets and sets the starting point of the bytes selected in the hex box.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long SelectionStart
        {
            get => _bytePos;
            set
            {
                SetPosition(value, 0);
                ScrollByteIntoView();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets and sets the number of bytes selected in the hex box.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long SelectionLength
        {
            get => _selectionLength;
            set
            {
                SetSelectionLength(value);
                ScrollByteIntoView();
                Invalidate();
            }
        }
        long _selectionLength;

        /// <summary>
        /// Contains the size of a single character in pixel.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SizeF CharSize
        {
            get => _charSize;
            private set
            {
                if (_charSize == value) return;
                _charSize = value;
                if (CharSizeChanged != null) CharSizeChanged(this, EventArgs.Empty);
            }
        }
        SizeF _charSize;

        /// <summary>
        /// Gets the width required for the content.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), DefaultValue(0)]
        public int RequiredWidth
        {
            get => _requiredWidth;
            private set
            {
                if (_requiredWidth == value) return;
                _requiredWidth = value;
                if (RequiredWidthChanged != null) RequiredWidthChanged(this, EventArgs.Empty);
            }
        }
        int _requiredWidth;

        /// <summary>
        /// Gets the number bytes drawn horizontally.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int HorizontalByteCount => _iHexMaxHBytes;

        /// <summary>
        /// Gets the number bytes drawn vertically.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int VerticalByteCount => _iHexMaxVBytes;

        /// <summary>
        /// Gets the current line
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentLine { get; private set; }

        /// <summary>
        /// Gets the current position in the current line
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentPositionInLine { get; private set; }

        /// <summary>
        /// Gets the a value if insertion mode is active or not.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool InsertActive
        {
            get => _insertActive;
            set
            {
                if (_insertActive == value) return;

                _insertActive = value;

                // recreate caret
                DestroyCaret();
                CreateCaret();

                // raise change event
                OnInsertActiveChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the converter that will translate between byte and character values.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IByteCharConverter ByteCharConverter
        {
            get
            {
                if (_byteCharConverter == null) _byteCharConverter = new DefaultByteCharConverter();
                return _byteCharConverter;
            }
            set
            {
                if (value == null || value == _byteCharConverter) return;
                _byteCharConverter = value;
                Invalidate();
            }
        }
        IByteCharConverter _byteCharConverter;
        #endregion

        #region Misc
        /// <summary>
        /// Converts a byte array to a hex string. For example: {10,11} = "0A 0B";
        /// </summary>
        /// <param name="data">the byte array</param>
        /// <param name="separator">Separator between hex and hex</param>
        /// <returns>the hex string</returns>
        string ConvertBytesToHex(byte[] data, string separator = "")
        {
            string result = BitConverter.ToString(data, 0, data.Length).Replace("-", separator);
            if (HexCasing == HexCasing.Lower) result = result.ToLower();

            return result;
        }

        /// <summary>
        /// Converts the hex string to an byte array. The hex string must be separated by a space char ' '. If there is any invalid hex information in the string the result will be null.
        /// </summary>
        /// <param name="hex">the hex string separated by ' '. For example: "0A 0B 0C"</param>
        /// <returns>the byte array. null if hex is invalid or empty</returns>
        byte[] ConvertHexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;

            hex = hex.Trim();
            var hexArray = hex.Split(' ');
            var byteArray = new byte[hexArray.Length];

            for (int i = 0; i < hexArray.Length; i++)
            {
                var hexValue = hexArray[i];

                var isByte = ConvertHexToByte(hexValue, out byte b);
                if (!isByte) return null;

                byteArray[i] = b;
            }

            return byteArray;
        }

        bool ConvertHexToByte(string hex, out byte b) => byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Threading.Thread.CurrentThread.CurrentCulture, out b);

        void SetPosition(long bytePos) => SetPosition(bytePos, _byteCharacterPos);

        void SetPosition(long bytePos, int byteCharacterPos)
        {
            if (_byteCharacterPos != byteCharacterPos) _byteCharacterPos = byteCharacterPos;

            if (bytePos != _bytePos)
            {
                _bytePos = bytePos;
                CheckCurrentLineChanged();
                CheckCurrentPositionInLineChanged();

                OnSelectionStartChanged(EventArgs.Empty);
            }
        }

        void SetSelectionLength(long selectionLength)
        {
            if (selectionLength != _selectionLength)
            {
                _selectionLength = selectionLength;
                OnSelectionLengthChanged(EventArgs.Empty);
            }
        }

        void SetHorizontalByteCount(int value)
        {
            if (_iHexMaxHBytes == value) return;

            _iHexMaxHBytes = value;
            OnHorizontalByteCountChanged(EventArgs.Empty);
        }

        void SetVerticalByteCount(int value)
        {
            if (_iHexMaxVBytes == value) return;

            _iHexMaxVBytes = value;
            OnVerticalByteCountChanged(EventArgs.Empty);
        }

        void CheckCurrentLineChanged()
        {
            long currentLine = (long)Math.Floor((double)_bytePos / (double)_iHexMaxHBytes) + 1;

            if (_byteProvider == null && CurrentLine != 0)
            {
                CurrentLine = 0;
                OnCurrentLineChanged(EventArgs.Empty);
            }
            else if (currentLine != CurrentLine)
            {
                CurrentLine = currentLine;
                OnCurrentLineChanged(EventArgs.Empty);
            }
        }

        void CheckCurrentPositionInLineChanged()
        {
            Point gb = GetGridBytePoint(_bytePos);
            int currentPositionInLine = gb.X + 1;

            if (_byteProvider == null && CurrentPositionInLine != 0)
            {
                CurrentPositionInLine = 0;
                OnCurrentPositionInLineChanged(EventArgs.Empty);
            }
            else if (currentPositionInLine != CurrentPositionInLine)
            {
                CurrentPositionInLine = currentPositionInLine;
                OnCurrentPositionInLineChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises the InsertActiveChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnInsertActiveChanged(EventArgs e)
        {
            if (InsertActiveChanged != null) InsertActiveChanged(this, e);
        }

        /// <summary>
        /// Raises the ReadOnlyChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnReadOnlyChanged(EventArgs e)
        {
            if (ReadOnlyChanged != null) ReadOnlyChanged(this, e);
        }

        /// <summary>
        /// Raises the ByteProviderChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnByteProviderChanged(EventArgs e)
        {
            if (ByteProviderChanged != null) ByteProviderChanged(this, e);
        }

        /// <summary>
        /// Raises the SelectionStartChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnSelectionStartChanged(EventArgs e)
        {
            if (SelectionStartChanged != null) SelectionStartChanged(this, e);
        }

        /// <summary>
        /// Raises the SelectionLengthChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnSelectionLengthChanged(EventArgs e)
        {
            if (SelectionLengthChanged != null) SelectionLengthChanged(this, e);
        }

        /// <summary>
        /// Raises the LineInfoVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnLineInfoVisibleChanged(EventArgs e)
        {
            if (LineInfoVisibleChanged != null) LineInfoVisibleChanged(this, e);
        }

        /// <summary>
        /// Raises the OnColumnInfoVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnColumnInfoVisibleChanged(EventArgs e)
        {
            if (ColumnInfoVisibleChanged != null) ColumnInfoVisibleChanged(this, e);
        }

        /// <summary>
        /// Raises the ColumnSeparatorVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnGroupSeparatorVisibleChanged(EventArgs e)
        {
            if (GroupSeparatorVisibleChanged != null) GroupSeparatorVisibleChanged(this, e);
        }

        /// <summary>
        /// Raises the StringViewVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnStringViewVisibleChanged(EventArgs e)
        {
            if (StringViewVisibleChanged != null) StringViewVisibleChanged(this, e);
        }

        /// <summary>
        /// Raises the BorderStyleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnBorderStyleChanged(EventArgs e)
        {
            if (BorderStyleChanged != null) BorderStyleChanged(this, e);
        }

        /// <summary>
        /// Raises the UseFixedBytesPerLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnUseFixedBytesPerLineChanged(EventArgs e)
        {
            if (UseFixedBytesPerLineChanged != null) UseFixedBytesPerLineChanged(this, e);
        }

        /// <summary>
        /// Raises the GroupSizeChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnGroupSizeChanged(EventArgs e)
        {
            if (GroupSizeChanged != null) GroupSizeChanged(this, e);
        }

        /// <summary>
        /// Raises the BytesPerLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnBytesPerLineChanged(EventArgs e)
        {
            if (BytesPerLineChanged != null) BytesPerLineChanged(this, e);
        }

        /// <summary>
        /// Raises the VScrollBarVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnVScrollBarVisibleChanged(EventArgs e)
        {
            if (VScrollBarVisibleChanged != null) VScrollBarVisibleChanged(this, e);
        }

        /// <summary>
        /// Raises the HexCasingChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHexCasingChanged(EventArgs e)
        {
            if (HexCasingChanged != null) HexCasingChanged(this, e);
        }

        /// <summary>
        /// Raises the HorizontalByteCountChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHorizontalByteCountChanged(EventArgs e)
        {
            if (HorizontalByteCountChanged != null) HorizontalByteCountChanged(this, e);
        }

        /// <summary>
        /// Raises the VerticalByteCountChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnVerticalByteCountChanged(EventArgs e)
        {
            if (VerticalByteCountChanged != null) VerticalByteCountChanged(this, e);
        }

        /// <summary>
        /// Raises the CurrentLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCurrentLineChanged(EventArgs e)
        {
            if (CurrentLineChanged != null) CurrentLineChanged(this, e);
        }

        /// <summary>
        /// Raises the CurrentPositionInLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCurrentPositionInLineChanged(EventArgs e)
        {
            if (CurrentPositionInLineChanged != null) CurrentPositionInLineChanged(this, e);
        }


        /// <summary>
        /// Raises the Copied event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCopied(EventArgs e)
        {
            if (Copied != null) Copied(this, e);
        }

        /// <summary>
        /// Raises the MouseDown event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnMouseDown()", "HexBox");

            if (!Focused) Focus();

            if (e.Button == MouseButtons.Left) SetCaretPosition(new Point(e.X, e.Y));

            base.OnMouseDown(e);
        }

        /// <summary>
        /// Raises the MouseWhell event
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int linesToScroll = -(e.Delta * SystemInformation.MouseWheelScrollLines / 120);
            PerformScrollLines(linesToScroll);

            base.OnMouseWheel(e);
        }


        /// <summary>
        /// Raises the Resize event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRectanglePositioning();
        }

        /// <summary>
        /// Raises the GotFocus event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnGotFocus(EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnGotFocus()", "HexBox");

            base.OnGotFocus(e);

            CreateCaret();
        }

        /// <summary>
        /// Raises the LostFocus event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnLostFocus(EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnLostFocus()", "HexBox");

            base.OnLostFocus(e);

            DestroyCaret();
        }

        void _byteProvider_LengthChanged(object sender, EventArgs e) => UpdateScrollSize();
        #endregion

        #region Scaling Support for High DPI resolution screens
        /// <summary>
        /// For high resolution screen support
        /// </summary>
        /// <param name="factor">the factor</param>
        /// <param name="specified">bounds</param>
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);

            BeginInvoke(new MethodInvoker(() =>
            {
                UpdateRectanglePositioning();
                if (_caretVisible)
                {
                    DestroyCaret();
                    CreateCaret();
                }
                Invalidate();
            }));
        }
        #endregion
    }
}