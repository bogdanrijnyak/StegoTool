using System;
using System.Drawing;

public class PSNRCalculator
{
    public static double CalculateMSE(Bitmap img1, Bitmap img2)
    {
        if (img1.Width != img2.Width || img1.Height != img2.Height)
            throw new ArgumentException("Розміри зображень не співпадають!");

        double mse = 0;
        for (int y = 0; y < img1.Height; y++)
        {
            for (int x = 0; x < img1.Width; x++)
            {
                Color c1 = img1.GetPixel(x, y);
                Color c2 = img2.GetPixel(x, y);

                //  Можна використовувати тільки яскравість, або кожну компоненту RGB
                double errorR = c1.R - c2.R;
                double errorG = c1.G - c2.G;
                double errorB = c1.B - c2.B;

                mse += (errorR * errorR + errorG * errorG + errorB * errorB);
            }
        }

        mse /= (img1.Width * img1.Height * 3);
        return mse;
    }

    public static double CalculatePSNR(Bitmap img1, Bitmap img2)
    {
        double mse = CalculateMSE(img1, img2);
        if (mse == 0)
            return double.PositiveInfinity;

        double max = 255.0;
        double psnr = 10.0 * Math.Log10((max * max) / mse);
        return psnr;
    }

}
