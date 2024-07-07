﻿using System.Drawing.Imaging;
using QRCoder;
using SixLabors.ImageSharp.Formats.Qoi;

namespace Application.Helper;

public static class GenerateQrCode
{
    public static async Task<byte[]> GetQrCodeAsync(string text)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrCodeData);
            using var qrCodeImage = qrCode.GetGraphic(20);

            // Save QR code image to memory stream
            using var ms = new MemoryStream();
            qrCodeImage.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            // Return image as byte array
            return ms.ToArray();
        }
        catch (System.Exception qr)
        {
            throw new ApplicationException("qr Kiry" + qr.Message);
        }
        // Generate QR code
    }
}