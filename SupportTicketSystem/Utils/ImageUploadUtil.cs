using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace SupportTicketSystem.Utils;

public class ImageUploadUtil
{
    private readonly IWebHostEnvironment _env;
    
    private IFormFile _image;
    private string _fileName;
    string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };


    public ImageUploadUtil(IFormFile image, string fileName, IWebHostEnvironment env)
    {
        _image = image;
        _fileName = fileName;
        _env = env;
    }

    private bool ValidateExtension(IFormFile file)
    {
        string extension = Path.GetExtension(file.FileName).ToLower();
        return _allowedExtensions.Contains(extension);
    }

    private string renameImage()
    {
        string fileExtension = Path.GetExtension(_image.FileName).ToLower();
        string newFileName = _fileName + "-image" + fileExtension;
        return newFileName;
    }

    private bool IsValidImage(Stream imageStream)
    {
        if (imageStream == null || imageStream.Length == 0)
            return false;

        // Raw magic byte signatures
        byte[][] signatures = new byte[][]
        {
            new byte[] { 0xFF, 0xD8 }, // JPEG
            new byte[] { 0x42, 0x4D }, // BMP
            new byte[] { 0x47, 0x49, 0x46 }, // GIF
            new byte[] { 0x89, 0x50, 0x4E, 0x47 } // PNG
        };

        byte[] header = new byte[4];

        imageStream.Read(header, 0, header.Length);

        bool hasKnownHeader = signatures.Any(sig =>
            header.Take(sig.Length).SequenceEqual(sig));

        if (!hasKnownHeader)
            return false;

        imageStream.Seek(0, SeekOrigin.Begin); // Reset for full decode
        return true;
    }

    public async Task<string> ValidateImage(IFormFile image)
    {
        if (ValidateExtension(image))
        {
            if (IsValidImage(image.OpenReadStream()))
            {
                string newFileName = renameImage();

                string folderPath = Path.Combine(_env.WebRootPath, "Images");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string savePath = Path.Combine(folderPath, newFileName);
                using (FileStream fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                return "/Images/" + newFileName;
            }
            throw new Exception("Invalid image format");
        }
        throw new Exception("Invalid image extension");
    }
}
    