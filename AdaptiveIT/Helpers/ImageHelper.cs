using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace AdaptiveIT.Helpers
{
    public static class ImageHelper
    {
        public static async Task<string> ProcessAndSaveImageAsync(IFormFile file, string webRootPath, string folderName, int maxWidth = 800)
        {
            var fileName = Guid.NewGuid().ToString() + ".jpg";
            var folderPath = Path.Combine(webRootPath, "uploads", folderName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);
            using var image = await Image.LoadAsync(file.OpenReadStream());

            if (image.Width > maxWidth)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, 0),
                    Mode = ResizeMode.Max
                }));
            }

            var encoder = new JpegEncoder { Quality = 80 };
            await image.SaveAsync(filePath, encoder);

            return $"/uploads/{folderName}/{fileName}";
        }

        public static void DeleteImageFile(string? imageUrl, string webRootPath)
        {
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var filePath = Path.Combine(webRootPath, imageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }
    }
}