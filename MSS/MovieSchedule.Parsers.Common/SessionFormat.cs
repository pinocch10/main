using System.ComponentModel;

namespace MovieSchedule.Parsers.Common
{
    public enum SessionFormat
    {
        [Description("2D")]
        TwoD = 0,
        [Description("3D")]
        ThreeD = 2,
        [Description("IMAX")]
        IMAX = 4,
        [Description("4DX")]
        FourDX = 8,
        [Description("DBOX")]
        DBOX = 16
    }
}