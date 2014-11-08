using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nVlc.LibVlcWrapper.Declarations.Enums;

namespace nVlc.LibVlcWrapper.Declarations.Extensions
{
	public static class EnumExtensions
	{
		public static ChromaType TryParseChromaType(this string chromaStr)
		{
			switch (chromaStr)
			{
				case "I420":
					return ChromaType.I420;
				case "NV12":
					return ChromaType.NV12;
				case "RGBA":
					return ChromaType.RGBA;
				case "RV15":
					return ChromaType.RV15;
				case "RV16":
					return ChromaType.RV16;
				case "RV24":
					return ChromaType.RV24;
				case "RV32":
					return ChromaType.RV32;
				case "UYVY":
					return ChromaType.UYVY;
				case "YUY2":
					return ChromaType.YUY2;
				case "YV12":
					return ChromaType.YV12;
			}

			throw new ArgumentException("Unsupported chroma type " + chromaStr);
		}

		public static SoundType TryParseSoundType(this string formatStr)
		{
			switch (formatStr)
			{
				case "S16N":
					return SoundType.S16N;
			}

			throw new ArgumentException("Unsupported sound type " + formatStr);
		}
	}
}
