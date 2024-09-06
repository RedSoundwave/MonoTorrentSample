using System.Diagnostics;

namespace MonoTorrentSample
{
    public class TopListener : TraceListener
    {
        private readonly int _capacity;
        private readonly LinkedList<string> _traces;

        public TopListener(int capacity)
        {
            _capacity = capacity;
            _traces = new LinkedList<string>();
        }

        public override void Write(string message)
        {
            lock (_traces)
                if (_traces.Last != null)
                    _traces.Last.Value += message;
        }

        public override void WriteLine(string message)
        {
            lock (_traces)
            {
                if (_traces.Count >= _capacity)
                    _traces.RemoveFirst();

                _traces.AddLast(message);
            }
        }

        public void ExportTo(TextWriter output)
        {
            lock (_traces)
                foreach (string s in _traces)
                    output.WriteLine(s);
        }
    }
}
