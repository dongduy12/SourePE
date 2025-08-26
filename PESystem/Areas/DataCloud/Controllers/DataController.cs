using Aspose.Slides;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing.Imaging;

namespace PESystem.Areas.DataCloud.Controllers
{
    [Route("api/data")]
    [ApiController]
    public class DataController : ControllerBase
    {

        private readonly string _basePath = "D:\\DataCloud";
        private readonly string _recycleBinPath = Path.Combine("D:\\DataCloud", "RecycleBin");

        // Endpoint để render PPT/PPTX thành hình ảnh
        [HttpGet("render-pptx")]
        public async Task<IActionResult> RenderPptx(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return BadRequest("Tham số đường dẫn trống hoặc không hợp lệ.");
                }
                // Ghi log đường dẫn thô
                Console.WriteLine($"Đường dẫn thô: {path}");
                string decodedPath = Uri.UnescapeDataString(path).Replace("/", "\\");
                if (!decodedPath.StartsWith("D:\\DataCloud\\", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Đường dẫn phải bắt đầu bằng 'D:\\DataCloud\\'.");
                }

                string filePath = Path.Combine(_basePath, decodedPath.Replace("D:\\DataCloud\\", ""));
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("File không tồn tại.");
                }

                // Tạo thư mục tạm để lưu hình ảnh
                string tempDir = Path.Combine(_basePath, "Temp", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Load file PPT/PPTX bằng Aspose.Slides
                using (var presentation = new Presentation(filePath))
                {
                    // Cấu hình kích thước hình ảnh
                    int width = 960; // Chiều rộng hình ảnh
                    int height = 720; // Chiều cao hình ảnh (tỷ lệ 4:3)

                    var slides = presentation.Slides;
                    var imageUrls = new List<string>();

                    for (int i = 0; i < slides.Count; i++)
                    {
                        string imagePath = Path.Combine(tempDir, $"slide-{i + 1}.png");
                        using (var image = slides[i].GetThumbnail(width, height))
                        {
                            image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        // Tạo URL để client truy cập hình ảnh
                        string imageUrl = $"/Temp/{Path.GetFileName(tempDir)}/slide-{i + 1}.png";
                        imageUrls.Add(imageUrl);
                    }

                    return Ok(new { slides = imageUrls });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi render PPTX: {ex}");
                return StatusCode(500, $"Lỗi khi render PPT/PPTX: {ex.Message}");
            }
        }

        // Endpoint để truy cập file download (đã có trong hệ thống của bạn)
        [HttpGet("download-file")]
        public IActionResult DownloadFile(string path)
        {
            string filePath = Path.Combine(_basePath, path.Replace("D:\\DataCloud\\", ""));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File không tồn tại.");
            }
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "application/octet-stream", Path.GetFileName(filePath));
        }
        //[HttpGet("download-file")]
        //public IActionResult DownloadFile(string path)
        //{
        //    try
        //    {
        //        if (!System.IO.File.Exists(path))
        //            return NotFound("Tệp không tồn tại!");

        //        var fileName = Path.GetFileName(path);
        //        var fileBytes = System.IO.File.ReadAllBytes(path);
        //        return File(fileBytes, "application/octet-stream", fileName);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Lỗi: {ex.Message}");
        //    }
        //}

        [HttpGet("get-data")]
        public IActionResult GetData(string path)
        {
            Console.WriteLine($"Đường dẫn nhận được: {path}");
            try
            {
                if (string.IsNullOrEmpty(path))
                    path = @"D:\DataCloud";

                if (!Directory.Exists(path))
                    return NotFound($"Thư mục {path} không tồn tại.");

                var folders = Directory.GetDirectories(path)
                    .Where(dir => !Path.GetFileName(dir).Equals("RecycleBin", StringComparison.OrdinalIgnoreCase))
                    .Select(dir => new
                    {
                        Name = Path.GetFileName(dir),
                        Path = dir,
                        Type = "Folder"
                    });

                var files = Directory.GetFiles(path).Select(file => new
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    Type = "File"
                });

                var items = folders.Concat(files).ToList();

                return Ok(new { CurrentPath = path, Items = items });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpPost("create-folder")]
        public IActionResult CreateFolder([FromForm] string path, [FromForm] string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(folderName))
                    return BadRequest("Tên thư mục không được để trống.");

                string newFolderPath = Path.Combine(path, folderName);
                if (Directory.Exists(newFolderPath))
                    return Conflict("Thư mục đã tồn tại.");

                Directory.CreateDirectory(newFolderPath);
                return Ok(new { Message = "Thư mục được tạo thành công.", FolderPath = newFolderPath });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpPost("upload-file")]
        public IActionResult UploadFile([FromForm] List<IFormFile> files, [FromForm] string path)
        {
            try
            {
                if (files == null || !files.Any() || string.IsNullOrEmpty(path))
                    return BadRequest("Tệp hoặc đường dẫn không hợp lệ!");

                foreach (var file in files)
                {
                    var fullPath = Path.Combine(path, file.FileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }
                return Ok(new { Message = "Tệp đã được tải lên thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpPost("upload-folder")]
        public IActionResult UploadFolder([FromForm] List<IFormFile> files, [FromForm] string path)
        {
            try
            {
                if (files == null || !files.Any() || string.IsNullOrEmpty(path))
                    return BadRequest("Thư mục hoặc đường dẫn không hợp lệ!");

                foreach (var file in files)
                {
                    var relativePath = file.FileName; // Sử dụng FileName tạm thời (cần webkitRelativePath từ client)
                    var fullPath = Path.Combine(path, relativePath);
                    var directory = Path.GetDirectoryName(fullPath);

                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }
                return Ok(new { Message = "Thư mục đã được tải lên thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpGet("download-folder")]
        public IActionResult DownloadFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return NotFound("Thư mục không tồn tại!");

                var folderName = Path.GetFileName(path);
                var tempZipPath = Path.Combine(Path.GetTempPath(), $"{folderName}.zip");

                if (System.IO.File.Exists(tempZipPath)) System.IO.File.Delete(tempZipPath);
                ZipFile.CreateFromDirectory(path, tempZipPath);

                var fileBytes = System.IO.File.ReadAllBytes(tempZipPath);
                System.IO.File.Delete(tempZipPath);

                return File(fileBytes, "application/zip", $"{folderName}.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        public class ItemModel
        {
            public string Path { get; set; }
            public string Type { get; set; }
        }

        [HttpPost("delete-items")]
        public IActionResult DeleteItems([FromBody] List<ItemModel> items)
        {
            try
            {
                Directory.CreateDirectory(_recycleBinPath);

                foreach (var item in items)
                {
                    if (!item.Path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
                        return BadRequest($"Đường dẫn không hợp lệ: {item.Path}");

                    var relative = Path.GetRelativePath(_basePath, item.Path);
                    var destination = Path.Combine(_recycleBinPath, relative);
                    destination = GetUniquePath(destination);

                    if (item.Type == "File" && System.IO.File.Exists(item.Path))
                    {
                        var destDir = Path.GetDirectoryName(destination);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir!);
                        System.IO.File.Move(item.Path, destination);
                    }
                    else if (item.Type == "Folder" && Directory.Exists(item.Path))
                    {
                        var destDir = Path.GetDirectoryName(destination);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir!);
                        Directory.Move(item.Path, destination);
                    }
                }

                return Ok(new { Message = "Đã chuyển vào thùng rác!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        private string GetUniquePath(string path)
        {
            if (!System.IO.File.Exists(path) && !Directory.Exists(path))
                return path;

            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            return Path.Combine(directory!, $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
        }

        // API tìm kiếm mới
        [HttpGet("search")]
        public IActionResult Search([FromQuery] string keyword, [FromQuery] string path = @"D:\DataCloud")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    return BadRequest(new { Message = "Từ khóa tìm kiếm không được để trống." });

                if (!Directory.Exists(path))
                    return NotFound(new { Message = $"Thư mục không tồn tại: {path}" });

                // Tìm kiếm file và folder
                var items = SearchItems(path, keyword);

                // Trả về kết quả
                return Ok(new
                {
                    CurrentPath = path,
                    Items = items.Select(item => new
                    {
                        Name = item.Name,
                        Path = item.FullName,
                        Type = item is DirectoryInfo ? "Folder" : "File"
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi khi tìm kiếm: {ex.Message}" });
            }
        }

        private bool IsApproxMatch(string source, string keyword)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(keyword))
                return false;

            if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;

            var distance = LevenshteinDistance(source.ToLower(), keyword.ToLower());
            var threshold = Math.Max(1, keyword.Length / 3);
            return distance <= threshold;
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        private List<FileSystemInfo> SearchItems(string path, string keyword)
        {
            var result = new List<FileSystemInfo>();
            try
            {
                var directory = new DirectoryInfo(path);
                if (directory.FullName.Equals(_recycleBinPath, StringComparison.OrdinalIgnoreCase))
                    return result;

                // Tìm kiếm file và folder trong thư mục hiện tại
                var directories = directory.GetDirectories("*", SearchOption.TopDirectoryOnly)
                    .Where(d => !d.Name.Equals("RecycleBin", StringComparison.OrdinalIgnoreCase) && IsApproxMatch(d.Name, keyword));

                var files = directory.GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => IsApproxMatch(f.Name, keyword));

                result.AddRange(directories);
                result.AddRange(files);

                // Tìm kiếm đệ quy trong các thư mục con
                foreach (var subDir in directory.GetDirectories())
                {
                    try
                    {
                        if (!subDir.Name.Equals("RecycleBin", StringComparison.OrdinalIgnoreCase))
                            result.AddRange(SearchItems(subDir.FullName, keyword));
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Bỏ qua nếu không có quyền truy cập
                        continue;
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi khác
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Bỏ qua lỗi nếu không truy cập được thư mục
            }

            return result;
        }


        [HttpPost("cleanup-temp")]
        public IActionResult CleanupTemp([FromBody] dynamic data)
        {
            try
            {
                string tempDir = Path.Combine(_basePath, data.tempDir.ToString());
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                return Ok("Đã xóa thư mục tạm.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi xóa thư mục tạm: {ex.Message}");
            }
        }
    }
}