using UnityEngine;

public class VolumeManager : MonoBehaviour
{
    private static VolumeManager instance = null;

    void Awake()
    {
        if (null == instance)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            instance.bgmVolume = 1f;
            instance.sfxVolume = 1f;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static VolumeManager Instance
    {
        get
        {
            if (null == instance)
            {
                return null;
            }
            return instance;
        }
    }

    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
}
