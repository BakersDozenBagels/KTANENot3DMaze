namespace ThreeDMaze
{
    sealed class MapView
    {
        public bool ble = false;
        public bool blw = false;
        public bool bre = false;
        public bool brw = false;
        public bool bw = false;
        public bool fle = false;
        public bool flw = false;
        public bool fre = false;
        public bool frw = false;
        public bool fw = false;

        public char letter = ' ';
        public bool letter_far = false;

        public bool VisiblyIdenticalTo(MapView other)
        {
            if (letter != other.letter)
                return false;
            if(fw != other.fw)
                return false;
            if (fle != other.fle)
                return false;
            if (fre != other.fre)
                return false;

            if (!fle && flw != other.flw)
                return false;
            if (!fre && frw != other.frw)
                return false;
            if (!fw && letter_far != other.letter_far)
                return false;
            if (!fw && bw != other.bw)
                return false;
            if (!fw && ble != other.ble)
                return false;
            if (!fw && bre != other.bre)
                return false;

            if ((!fle && !flw || !fw && !ble) && blw != other.blw)
                return false;
            if ((!fre && !frw || !fw && !bre) && brw != other.brw)
                return false;

            return true;
        }
    }
}