using UnityEngine;
using System.IO;

namespace MoreStatistics
{
    //Static class for ease of access
    public static class Assets
    {
        //The mod's AssetBundle
        public static AssetBundle mainBundle;
        //A constant of the AssetBundle's name.
        public const string bundleName = "nullos.statstriptemplate";
        public const string assetBundleFolder = "assetbundles";

        //The direct path to your AssetBundle
        public static string AssetBundlePath
        {
            get
            {
                //This returns the path to your assetbundle assuming said bundle is on the same folder as your DLL.
                return Path.Combine(Path.GetDirectoryName(MoreStatistics.PInfo.Location), assetBundleFolder, bundleName);
            }
        }

        public static void Init()
        {
            //Loads the assetBundle from the Path, and stores it in the static field.
            mainBundle = AssetBundle.LoadFromFile(AssetBundlePath);
        }
    }
}