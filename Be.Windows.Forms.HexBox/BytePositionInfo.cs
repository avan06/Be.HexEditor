﻿namespace Be.Windows.Forms
{
    /// <summary>
    /// Represents a position in the HexBox control
    /// </summary>
    struct BytePositionInfo
    {
        public BytePositionInfo(long index, int characterPosition)
        {
            _index = index;
            _characterPosition = characterPosition;
        }

        public int CharacterPosition => _characterPosition;
        int _characterPosition;

        public long Index => _index;
        long _index;
    }
}
