using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;


namespace DDStronk
{
    using Pfim;
    using System.Collections.Generic;
    using System.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            using var bc = new BulkConverter();
            foreach (var arg in args)
            {
                if (Directory.Exists(arg))
                {
                    foreach (var dds in Directory.EnumerateFiles(arg, "*.dds", SearchOption.AllDirectories))
                    {
                        bc.Add(dds);
                    }
                }
                else
                {
                    bc.Add(arg);
                }
            }
        }
    }

    public sealed class BulkConverter : IDisposable
    {
        private readonly BlockingCollection<string> _images = new();
        private readonly Task _processTask;

        public BulkConverter() => _processTask = Task.Run(ProcessLoop);

        public void Add(string path) => _images.Add(path ?? throw new ArgumentNullException(nameof(path)));

        private void ProcessLoop()
        {
            List<Exception> conversionErrors = new();
            foreach (var path in _images.GetConsumingEnumerable())
            {
                try
                {
                    Convert(path);
                    Console.WriteLine("Converted {0}", path);
                }
                catch (Exception inner)
                {
                    conversionErrors.Add(inner);
                }
            }

            if (conversionErrors.Any())
            {
                Console.WriteLine("Finished with errors");
                throw new AggregateException("Errors during bulk conversion", conversionErrors);
            }
            else
            {
                Console.WriteLine("Finished successfully");
            }
        }

        private static unsafe void Convert(string path)
        {
            using var image = Pfim.FromFile(path);

            var format = image.Format switch
            {
                ImageFormat.Rgba32 => PixelFormat.Format32bppArgb,
                _ => throw new NotImplementedException(),// see the sample for more details
            };

            fixed (byte* ptr = image.Data)
            {
                using var bitmap = new Bitmap(image.Width, image.Height, image.Stride, format, (IntPtr)ptr);
                bitmap.Save(Path.ChangeExtension(path, ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (_images)
                using (_processTask)
                {
                    _images.CompleteAdding();
                    _processTask.Wait();
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
