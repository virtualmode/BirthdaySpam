// ---------------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="HttpServer.cs" company="Elcomplus LLC"></copyright>
// <date>2018-06-19</date>
// ---------------------------------------------------------------------------------------------------------------------------------------------------
namespace BirthdaySpam.Code
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;

    public class HttpServer
    {
        #region Constants

        public const string HTTP_PREFIX = "http://*:{0}/";
        public const string HTTPS_PREFIX = "https://*:{0}/";

        #endregion Constants

        #region Events

        public delegate void HttpRequestHandler(HttpListenerContext context);
        public event HttpRequestHandler OnHttpRequest;

        #endregion Events

        #region Fields

        private bool _started;
        private readonly int _httpPort;
        private readonly int _httpsPort;
        private readonly HttpListener _listener;
        private Thread _listenerThread;

        #endregion Fields

        #region Constructors

        public HttpServer(int httpPort, int httpsPort)
        {
            _started = false;
            _listener = new HttpListener();
            _listenerThread = null;
            _httpPort = httpPort;
            _httpsPort = httpsPort;
        }

        #endregion Constructors

        #region Methods

        public void Start()
        {
            if (!HttpListener.IsSupported)
                return;

            _listener.Prefixes.Add(string.Format(HTTP_PREFIX, _httpPort));
            //_listener.Prefixes.Add(string.Format(HTTPS_PREFIX, _httpsPort)); // TODO: Not to use this feature.

            _listener.Start();
            _started = true;

            _listenerThread = new Thread(Listen);
            _listenerThread.Start();
        }

        public void Stop()
        {
            _started = false; // Set flag first to terminate listener appropriate way.

            _listener.Stop();
            if (_listenerThread != null)
            {
                _listenerThread.Join();
                _listenerThread = null;
            }
        }

        /// <summary>
        /// Handle HTTP request.
        /// </summary>
        /// <param name="result"></param>
        private void HttpRequest(IAsyncResult result)
        {
            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                HttpListenerContext context = listener.EndGetContext(result);
                OnHttpRequest?.Invoke(context);
            }
            catch
            {
                // TODO: Can't handle request.
            }
        }

        private void Listen()
        {
            try
            {
                while (_started && _listener.IsListening)
                {
                    IAsyncResult result = _listener.BeginGetContext(new AsyncCallback(HttpRequest), _listener);
                    result.AsyncWaitHandle.WaitOne();
                }
            }
            catch
            {
                // TODO: Nothing to do.
            }
        }

        public static void WriteFile(HttpListenerContext context, string fileName)
        {
            using (Stream input = new FileStream(fileName, FileMode.Open))
            {
                context.Response.ContentType = HttpMime.GetMimeString(fileName);
                context.Response.ContentLength64 = input.Length;
                context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                context.Response.AddHeader("Last-Modified", File.GetLastWriteTime(fileName).ToString("r"));

                int nbytes;
                byte[] buffer = new byte[1024 * 32];
                while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                    context.Response.OutputStream.Write(buffer, 0, nbytes);
                input.Close();
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
        }

        [Obsolete]
        public static void SaveFile(HttpListenerContext context, string fileName)
        {
            var response = context.Response;
            using (FileStream fs = File.OpenRead(fileName))
            {
                response.ContentLength64 = fs.Length;
                response.SendChunked = false;
                response.ContentType = HttpMime.GetMimeString(fileName);
                response.AddHeader("Content-disposition", "attachment; filename=" + Path.GetFileName(fileName));

                byte[] buffer = new byte[64 * 1024];
                using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
                {
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        bw.Write(buffer, 0, read);
                    }

                    bw.Flush();
                    bw.Close();
                }

                response.StatusCode = (int)HttpStatusCode.OK;
                response.StatusDescription = "OK";
                response.OutputStream.Close();
            }
        }

        #endregion Methods
    }
}
