//    nVLC
//    
//    Author:  Roman Ginzburg
//
//    nVLC is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    nVLC is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//    GNU General Public License for more details.
//     
// ========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using nVlc.LibVlcWrapper.Declarations;
using nVlc.LibVlcWrapper.Declarations.Discovery;
using nVlc.LibVlcWrapper.Declarations.Media;
using nVlc.LibVlcWrapper.Declarations.Players;
using nVlc.LibVlcWrapper.Declarations.VLM;
using nVlc.LibVlcWrapper.Implementation.Discovery;
using nVlc.LibVlcWrapper.Implementation.Exceptions;
using nVlc.LibVlcWrapper.Implementation.Media;
using nVlc.LibVlcWrapper.Implementation.Players;
using nVlc.LibVlcWrapper.Implementation.VLM;
using nVlc.LibVlcWrapper;
using Microsoft.Win32;

namespace nVlc.LibVlcWrapper.Implementation
{
    /// <summary>
    /// Entry point for the nVLC library.
    /// </summary>
    public class MediaPlayerFactory : DisposableBase, IMediaPlayerFactory, IReferenceCount, INativePointer
    {
        IntPtr m_hMediaLib = IntPtr.Zero;
        Log m_log;
        IVideoLanManager m_vlm = null;
        NLogger m_logger = new NLogger();

        IntPtr m_pLock, m_pUnlock;
        List<Delegate> m_callbacks = new List<Delegate>();
            
        /// <summary>
        /// Initializes media library with default arguments
        /// </summary>
        public MediaPlayerFactory(string libvlcPath)
        {
            string[] args = new string[] 
             {
                "-I", 
                "dumy",  
                "--ignore-config", 
                "--no-osd",
                "--disable-screensaver",
                "--ffmpeg-hw",
                "--plugin-path=./plugins" 
             };

            Initialize(args, libvlcPath);
        }

        /// <summary>
        /// Initializes media library with user defined arguments
        /// </summary>
        /// <param name="args">Collection of arguments passed to libVLC library</param>
        /// <param name="findLibvlc">True to find libvlc installation path, False to use libvlc in the executable path</param>
        public MediaPlayerFactory(string[] args, string libvlcPath)
        {
            Initialize(args, libvlcPath);
        }


        private void Initialize(string[] args, string libvlcPath)
        {
            // Save current directory, since it probably will be changed
            var dir = Directory.GetCurrentDirectory();

            if (!string.IsNullOrEmpty(libvlcPath))
            {
                Directory.SetCurrentDirectory(libvlcPath);
            }

            try
            {
                m_hMediaLib = LibVlcMethods.libvlc_new(args.Length, args);
            }
            catch (DllNotFoundException ex)
            {
                throw new LibVlcNotFoundException(ex);
            }

            if (m_hMediaLib == IntPtr.Zero)
            {
                throw new LibVlcInitException();
            }
           
            m_log = new Log(m_hMediaLib, m_logger);
            m_log.Enabled = true;

            Directory.SetCurrentDirectory(dir);
        }

        /// <summary>
        /// Creates new instance of player.
        /// </summary>
        /// <typeparam name="T">Type of the player to create</typeparam>
        /// <returns>Newly created player</returns>
        public T CreatePlayer<T>() where T : IPlayer
        {
            return ObjectFactory.Build<T>(m_hMediaLib);
        }

        /// <summary>
        /// Creates new instance of media list player
        /// </summary>
        /// <typeparam name="T">Type of media list player</typeparam>
        /// <param name="mediaList">Media list</param>
        /// <returns>Newly created media list player</returns>
        public T CreateMediaListPlayer<T>(IMediaList mediaList) where T : IMediaListPlayer
        {
            return ObjectFactory.Build<T>(m_hMediaLib, mediaList);
        }

        /// <summary>
        /// Creates new instance of media.
        /// </summary>
        /// <typeparam name="T">Type of media to create</typeparam>
        /// <param name="input">The media input string</param>
        /// <param name="options">Optional media options</param>
        /// <returns>Newly created media</returns>
        public T CreateMedia<T>(string input, params string[] options) where T : IMedia
        {
            T media = ObjectFactory.Build<T>(m_hMediaLib);
            media.Input = input;
            media.AddOptions(options);

            return media;
        }

        /// <summary>
        /// Creates new instance of media list.
        /// </summary>
        /// <typeparam name="T">Type of media list</typeparam>
        /// <param name="mediaItems">Collection of media inputs</param>       
        /// <param name="options"></param>
        /// <returns>Newly created media list</returns>
        public T CreateMediaList<T>(IEnumerable<string> mediaItems, params string[] options) where T : IMediaList
        {
            T mediaList = ObjectFactory.Build<T>(m_hMediaLib);
            foreach (var file in mediaItems)
            {
                mediaList.Add(this.CreateMedia<IMedia>(file, options));
            }

            return mediaList;
        }

        /// <summary>
        /// Creates media list instance with no media items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T CreateMediaList<T>() where T : IMediaList
        {
            return ObjectFactory.Build<T>(m_hMediaLib);
        }

        /// <summary>
        /// Creates media discovery object
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IMediaDiscoverer CreateMediaDiscoverer(string name)
        {
            return ObjectFactory.Build<IMediaDiscoverer>(m_hMediaLib, name);
        }


        /// <summary>
        /// Gets the libVLC version.
        /// </summary>
        public string Version
        {
            get
            {
                return LibVlcMethods.libvlc_get_version();
            }
        }

        protected override void Dispose(bool disposing)
        {
            Release();
        }

        private static class ObjectFactory
        {
            static Dictionary<Type, Type> objectMap = new Dictionary<Type, Type>();

            static ObjectFactory()
            {
                objectMap.Add(typeof(IMedia), typeof(BasicMedia));
                objectMap.Add(typeof(IMediaFromFile), typeof(MediaFromFile));
                objectMap.Add(typeof(IVideoInputMedia), typeof(VideoInputMedia));
                objectMap.Add(typeof(IScreenCaptureMedia), typeof(ScreenCaptureMedia));
                objectMap.Add(typeof(IPlayer), typeof(BasicPlayer));
                objectMap.Add(typeof(IAudioPlayer), typeof(AudioPlayer));
                objectMap.Add(typeof(IVideoPlayer), typeof(VideoPlayer));
                objectMap.Add(typeof(IDiskPlayer), typeof(DiskPlayer));
                objectMap.Add(typeof(IMediaList), typeof(MediaList));
                objectMap.Add(typeof(IMediaListPlayer), typeof(MediaListPlayer));
                objectMap.Add(typeof(IVideoLanManager), typeof(VideoLanManager));
                objectMap.Add(typeof(IMediaDiscoverer), typeof(MediaDiscoverer));
            }

            public static T Build<T>(params object[] args)
            {
                if (objectMap.ContainsKey(typeof(T)))
                {
                    return (T)Activator.CreateInstance(objectMap[typeof(T)], args);
                }

                throw new ArgumentException("Unregistered type", typeof(T).FullName);
            }
        }

        #region IReferenceCount Members

        public void AddRef()
        {
            LibVlcMethods.libvlc_retain(m_hMediaLib);
        }

        public void Release()
        {
            try
            {
                LibVlcMethods.libvlc_release(m_hMediaLib);
            }
            catch (AccessViolationException)
            { }
        }

        #endregion

        #region INativePointer Members

        public IntPtr Pointer
        {
            get
            {
                return m_hMediaLib;
            }
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public long Clock
        {
            get
            {
                return LibVlcMethods.libvlc_clock();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pts"></param>
        /// <returns></returns>
        public long Delay(long pts)
        {
            return LibVlcMethods.libvlc_delay(pts);
        }

        /// <summary>
        /// Gets list of available audio filters
        /// </summary>
        public IEnumerable<FilterInfo> AudioFilters
        {
            get
            {
                IntPtr pList = LibVlcMethods.libvlc_audio_filter_list_get(m_hMediaLib);
                libvlc_module_description_t item = (libvlc_module_description_t)Marshal.PtrToStructure(pList, typeof(libvlc_module_description_t));

                do
                {
                    yield return GetFilterInfo(item);
                    if (item.p_next != IntPtr.Zero)
                    {
                        item = (libvlc_module_description_t)Marshal.PtrToStructure(item.p_next, typeof(libvlc_module_description_t));
                    }
                    else
                    {
                        break;
                    }

                }
                while (true);

                LibVlcMethods.libvlc_module_description_list_release(pList);
            }
        }

        /// <summary>
        /// Gets list of available video filters
        /// </summary>
        public IEnumerable<FilterInfo> VideoFilters
        {
            get
            {
                IntPtr pList = LibVlcMethods.libvlc_video_filter_list_get(m_hMediaLib);
                if (pList == IntPtr.Zero)
                {
                    yield break;
                }

                libvlc_module_description_t item = (libvlc_module_description_t)Marshal.PtrToStructure(pList, typeof(libvlc_module_description_t));

                do
                {
                    yield return GetFilterInfo(item);
                    if (item.p_next != IntPtr.Zero)
                    {
                        item = (libvlc_module_description_t)Marshal.PtrToStructure(item.p_next, typeof(libvlc_module_description_t));
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);

                LibVlcMethods.libvlc_module_description_list_release(pList);
            }
        }

        private FilterInfo GetFilterInfo(libvlc_module_description_t item)
        {
            return new FilterInfo()
            {
                Help = Marshal.PtrToStringAnsi(item.psz_help),
                Longname = Marshal.PtrToStringAnsi(item.psz_longname),
                Name = Marshal.PtrToStringAnsi(item.psz_name),
                Shortname = Marshal.PtrToStringAnsi(item.psz_shortname)
            };
        }

        /// <summary>
        /// Gets the VLM instance
        /// </summary>
        public IVideoLanManager VideoLanManager
        {
            get
            {
                if (m_vlm == null)
                {
                    m_vlm = ObjectFactory.Build<IVideoLanManager>(m_hMediaLib);
                }

                return m_vlm;
            }
        } 

        /// <summary>
        /// Gets list of available audio output modules
        /// </summary>
        public IEnumerable<AudioOutputModuleInfo> AudioOutputModules
        {
            get
            {
                IntPtr pList = LibVlcMethods.libvlc_audio_output_list_get(m_hMediaLib);
                libvlc_audio_output_t pDevice = (libvlc_audio_output_t)Marshal.PtrToStructure(pList, typeof(libvlc_audio_output_t));

                do
                {
                    AudioOutputModuleInfo info = GetDeviceInfo(pDevice);

                    yield return info;
                    if (pDevice.p_next != IntPtr.Zero)
                    {
                        pDevice = (libvlc_audio_output_t)Marshal.PtrToStructure(pDevice.p_next, typeof(libvlc_audio_output_t));
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);

                LibVlcMethods.libvlc_audio_output_list_release(pList);
            }
        }

        /// <summary>
        /// Gets list of available audio output devices
        /// </summary>
        public IEnumerable<AudioOutputDeviceInfo> GetAudioOutputDevices(AudioOutputModuleInfo audioOutputModule)
        {
            int i = LibVlcMethods.libvlc_audio_output_device_count(m_hMediaLib, audioOutputModule.Name.ToUtf8());
            for (int j = 0; j < i; j++)
            {
                AudioOutputDeviceInfo d = new AudioOutputDeviceInfo();
                d.Longname = LibVlcMethods.libvlc_audio_output_device_longname(m_hMediaLib, audioOutputModule.Name.ToUtf8(), j);
                d.Id = LibVlcMethods.libvlc_audio_output_device_id(m_hMediaLib, audioOutputModule.Name.ToUtf8(), j);

                yield return d;
            }
        }

        private AudioOutputModuleInfo GetDeviceInfo(libvlc_audio_output_t pDevice)
        {
            return new AudioOutputModuleInfo()
            {
                Name = Marshal.PtrToStringAnsi(pDevice.psz_name),
                Description = Marshal.PtrToStringAnsi(pDevice.psz_description)
            };
        }

        /// <summary>
        /// Attempt to locate the installation path of VLC, and set working diretory
        /// </summary>
        private void TrySetVLCPath()
        {
            try
            {
                TrySetVLCPath("vlc media player");
            }
            catch (Exception ex)
            {
                m_logger.Error("Failed to set VLC path: " + ex.Message);
            }
        }

        /// <summary>
        /// Attempt to locate the installation path of VLC, and set working diretory
        /// </summary>
        /// <param name="vlcRegistryKey">Default value is "vlc media player"</param>
        private void TrySetVLCPath(string vlcRegistryKey)
        {
            using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                foreach (string skName in rk.GetSubKeyNames())
                {
                    using (RegistryKey sk = rk.OpenSubKey(skName))
                    {
                        object DisplayName = sk.GetValue("DisplayName");
                        if (DisplayName != null)
                        {
                            if (DisplayName.ToString().ToLower().IndexOf(vlcRegistryKey.ToLower()) > -1)
                            {
                                object vlcDir = sk.GetValue("InstallLocation");

                                if (vlcDir != null)
                                {
                                    Directory.SetCurrentDirectory(vlcDir.ToString());
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
