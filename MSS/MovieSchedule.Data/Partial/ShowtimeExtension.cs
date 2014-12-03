using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MovieSchedule.Data.Helpers;

namespace MovieSchedule.Data
{
    public partial class Showtime
    {
        private List<string> _sessionsArray = null;
        public List<string> SessionsCollection
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SessionsRaw)) return new List<string>();
                if (_sessionsArray == null)
                    _sessionsArray = SessionsRaw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                return _sessionsArray;
            }
            set
            {
                _sessionsArray = value;
                if (_sessionsArray != null)
                    _sessionsArray = _sessionsArray.OrderBy(x => x, DataHelper.SessionsComparer).ToList();
                var sb = new StringBuilder();
                foreach (var session in _sessionsArray)
                {
                    sb.AppendFormat("{0}; ", session);
                }
                SessionsCount = _sessionsArray.Count;
                SessionsRaw = sb.ToString().Trim(' ', ';');
            }
        }
    }

    public partial class ShowtimeSnapshot
    {
        private List<string> _sessionsArray = null;
        public List<string> SessionsCollection
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Sessions)) return new List<string>();
                if (_sessionsArray == null)
                    _sessionsArray = Sessions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                return _sessionsArray;
            }
            set
            {
                _sessionsArray = value;
                var sb = new StringBuilder();
                foreach (var session in _sessionsArray)
                {
                    sb.AppendFormat("{0}; ", session);
                }
                SessionsCount = _sessionsArray.Count;
                Sessions = sb.ToString().Trim(' ', ';');
            }
        }
    }
}
