using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public class ImportAssetBundle : MonoBehaviour
{
    public static ImportAssetBundle Instance;

    private AssetBundle m_MainBundle = null;

    private AssetBundleManifest m_Manifest = null;

    private Dictionary<string, AssetBundle> m_Bundles = new Dictionary<string, AssetBundle>();

    private Dictionary<string, Object> m_Assets = new Dictionary<string, Object>();

    public string strABUrl
    {
        get
        {
            return Application.streamingAssetsPath + "/";
        }
    }

    public string strMainBundleName
    {
        get
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#else
            return "PC";
#endif
        }
    }

    public bool IsAnycLoadBundle = false;

    public bool IsLoadedMainBundle = false;

    public Object LoadRes(string strBundle, string strRes)
    {
        if (m_Assets.ContainsKey(strRes))
        {
            return m_Assets[strRes];
        }

        if (!LoadBundle(strBundle, strRes))
        {
            Debug.Log("AB包加载失败！");
            return null;
        }

        if (m_Bundles.ContainsKey(strBundle))
        {
            if (IsAnycLoadBundle)
            {
                StartCoroutine(AsyncLoadAsset(strBundle, strRes, obj =>
                {
                    if (!m_Assets.ContainsKey(obj.name))
                    {
                        m_Assets.Add(obj.name, obj);
                        Instantiate(obj);
                    }
                }));
            }
            else
            {
				Object ret = m_Bundles[strBundle].LoadAsset(strRes);
				if (ret == null)
				{
					Debug.Log("未在AB包中找到资源对象");
					return null;
				}
				m_Assets[strRes] = ret;
				return ret;
			}
		}

		return null;
    }

    public bool LoadBundle(string strBundleName, string strRes)
    {
        if (string.IsNullOrEmpty(strBundleName))
        {
            Debug.Log("传入要加载的包名非法！");
            return false;
        }

        if (m_Bundles.ContainsKey(strBundleName))
        {
            return true;
        }

        if (!IsLoadedMainBundle)
        {
			if (m_MainBundle == null)
			{
				m_MainBundle = AssetBundle.LoadFromFile(strABUrl + strMainBundleName);
			}
			if (m_Manifest == null)
			{
				if (m_MainBundle.Contains("AssetBundleManifest") == false)
				{
					Debug.Log("无依赖配置文件，AB包或已损坏");
					return false;
				}
				m_Manifest = m_MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
			}

			IsLoadedMainBundle = true;
		}

		if (!m_Bundles.ContainsKey(strBundleName))
        {
            if (IsAnycLoadBundle)
            {
                StartCoroutine(AsyncLoadBundle(strBundleName, strRes, AddBundleAndInstantiate));
            }
            else
            {
				AssetBundle bundle = AssetBundle.LoadFromFile(strABUrl + strBundleName);
                AddBundle(bundle);
			}
		}

        return true;
    }

    public bool LoadDependBundle(string strBundleName)
    {
        if (string.IsNullOrEmpty (strBundleName))
        {
            Debug.LogError("加载依赖包名非法！");
            return false;
        }
        if (m_Manifest == null)
        {
            Debug.LogError("m_Manifest == null");
            return false;
        }

        string[] strDependBundle = m_Manifest.GetAllDependencies(strBundleName);
        if (strBundleName.Length == 0)
        {
            Debug.Log(strBundleName + "无依赖包文件");
            return true;
        }

        for (int i = 0; i < strDependBundle.Length; i++)
        {
			string strBundle = strDependBundle[i];
			if (m_Bundles.ContainsKey(strBundle))
            {
                continue;
            }

            if (IsAnycLoadBundle)
            {
                StartCoroutine(AsyncLoadBundle(strBundle, AddBundle));
            }
            else
            {
				AssetBundle bundle = AssetBundle.LoadFromFile(strABUrl + strBundle);
				if (bundle != null)
				{
					m_Bundles.Add(strBundle, bundle);
				}
			}
		}

        return true;
    }

    IEnumerator AsyncLoadBundle(string strBundleName, UnityAction<AssetBundle> CallbackFunc)
    {
        AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(strABUrl + strBundleName);
        yield return bundleRequest;
        if (bundleRequest.isDone)
        {
			CallbackFunc(bundleRequest.assetBundle);
		}
        yield return null;
    }

	IEnumerator AsyncLoadBundle(string strBundleName, string strRes, UnityAction<AssetBundle, string> CallbackFunc)
	{
		AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(strABUrl + strBundleName);
		yield return bundleRequest;
		if (bundleRequest.isDone)
		{
			CallbackFunc(bundleRequest.assetBundle, strRes);
		}
		yield return null;
	}

	IEnumerator AsyncLoadAsset(string strBundleName, string strAssetName, UnityAction<Object> CallbackFunc)
    {
		AssetBundleRequest assetRequest = m_Bundles[strBundleName].LoadAssetAsync(strAssetName);
        yield return assetRequest;
        if (assetRequest.isDone)
        {
            CallbackFunc(assetRequest.asset);
        }
        yield return null;
	}

	public void AddBundle(AssetBundle bundle)
    {
        if (bundle == null)
        {
            return;
        }
        if (m_Bundles.ContainsKey(bundle.name) == false)
        {
            m_Bundles.Add(bundle.name, bundle);
        }

        LoadDependBundle(bundle.name);
	}

    public void AddBundleAndInstantiate(AssetBundle bundle, string strRes)
    {
        AddBundle(bundle);

        if (bundle != null && string.IsNullOrEmpty(strRes) == false)
        {
            StartCoroutine(AsyncLoadAsset(bundle.name, strRes, obj =>
            {
                if (obj is GameObject)
                {
                    Instantiate(obj);
                }
            }));
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        
        string md5Name = Path.Combine(strABUrl, "MD5.json");
        if (File.Exists(md5Name))
        {
            File.Delete(md5Name);
        }
        string metaFileName = md5Name + ".meta";
        if (File.Exists(metaFileName))
        {
            File.Delete(metaFileName);
        }
    }

    private void Start()
    {
        Dictionary<string, string> md5Bundles = new();
        var fileNames = Directory.GetFiles(strABUrl);
        var md5 = MD5.Create();
        StringBuilder strBuilder = new();
        foreach (var name in fileNames)
        {
            if (Path.GetExtension(name) == ".manifest" || Path.GetExtension(name) == ".meta")
            {
                continue;
            }

            var fileStr = File.OpenRead(name);
            if (fileNames.Length == 0)
            {
                continue;
            }

            byte[] hashStr = md5.ComputeHash(fileStr);
            foreach (var c in hashStr)
            {
                strBuilder.Append(c.ToString("x2"));
            }
            if (strBuilder.Length > 0 && !md5Bundles.ContainsKey(name))
            {
                md5Bundles.Add(name.Substring(name.LastIndexOf("/") + 1), strBuilder.ToString());
            }
        }

        strBuilder.Clear();
        foreach (var md in md5Bundles)
        {
            strBuilder.AppendLine($"{md.Key}: {md.Value}");
        }

        string md5Name = Path.Combine(strABUrl, "MD5.json");
        if (strBuilder.Length > 0)
        {
            using (StreamWriter jsonWriter = File.CreateText(md5Name))
            {
                jsonWriter.Write(strBuilder.ToString());
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            GameObject obj = LoadRes("pref", "Cube") as GameObject;
            if (obj != null) Instantiate(obj);
        }
    }
}
