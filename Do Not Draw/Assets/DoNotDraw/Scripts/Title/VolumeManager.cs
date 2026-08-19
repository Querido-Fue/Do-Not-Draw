using UnityEngine;

public class VolumeManager : MonoBehaviour
{
    private static VolumeManager instance = null;

    void Awake()
    {
        if (null == instance)
        {
            instance = this;
            this.bgmVolume = 1f;
            this.sfxVolume = 1f;

            DontDestroyOnLoad(this.gameObject);
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

    public float bgmVolume;
    public float sfxVolume;
}

// 전체 bgm, sfx 볼륨 저장 클래스