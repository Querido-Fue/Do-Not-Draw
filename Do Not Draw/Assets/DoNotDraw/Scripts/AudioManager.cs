using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SFX
{
    Footstep,
    Draw,
    Light,
    Spooky,
    Door

}

public class AudioManager : MonoBehaviour
{
    [Serializable]
    private struct SFXEntry
    {
        public SFX sfx;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("SFX Library")]
    [Tooltip("Footstep은 여기서 관리하지 않습니다. 아래 Footstep 전용 설정을 사용하세요.")]
    [SerializeField] private SFXEntry[] sfxLibrary = Array.Empty<SFXEntry>();

    [Header("Footstep (무작위 다중 클립 전용)")]
    [SerializeField] private AudioClip[] footstepClips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.55f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.94f, 1.06f);

    [Header("Channels")]
    [SerializeField, Min(1)] private int channelCount = 8;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

    private static AudioManager instance = null;

    private Dictionary<SFX, SFXEntry> sfxLookup;
    private AudioSource[] channels;
    private SFX[] channelSfx;
    private bool[] channelLooping;
    private Coroutine[] channelLoopRoutines;
    private int nextChannel;
    private int lastFootstepClipIndex = -1;

    void Awake()
    {
        if (null == instance)
        {
            instance = this;

            DontDestroyOnLoad(this.gameObject);

            BuildLookup();
            BuildChannels();
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static AudioManager Instance
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

    private void BuildLookup()
    {
        sfxLookup = new Dictionary<SFX, SFXEntry>(sfxLibrary.Length);

        foreach (SFXEntry entry in sfxLibrary)
        {
            if (null == entry.clip)
            {
                continue;
            }

            sfxLookup[entry.sfx] = entry;
        }
    }

    private void BuildChannels()
    {
        channels = new AudioSource[Mathf.Max(1, channelCount)];
        channelSfx = new SFX[channels.Length];
        channelLooping = new bool[channels.Length];
        channelLoopRoutines = new Coroutine[channels.Length];

        for (int i = 0; i < channels.Length; i++)
        {
            GameObject channelObject = new GameObject($"SFXChannel_{i}");
            channelObject.transform.SetParent(transform);

            AudioSource source = channelObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;

            channels[i] = source;
        }
    }

    /// <summary>
    /// 특정 위치에 sfx를 재생합니다.
    /// sfx가 enum형태로 저장되어 있으니 확인하여 사용하세요. (SFX.***)
    /// Footstep은 등록된 클립 중 무작위로 하나를 골라 재생합니다.
    /// </summary>
    public void PlaySFX(SFX sfx, Vector3 place)
    {
        if (SFX.Footstep == sfx)
        {
            PlayFootstep(place);
            return;
        }

        if (null == sfxLookup || !sfxLookup.TryGetValue(sfx, out SFXEntry entry) || null == entry.clip)
        {
            Debug.LogWarning($"[AudioManager] Missing SFX entry or clip for '{sfx}'.");
            return;
        }

        int channelIndex = GetFreeChannelIndex();
        AudioSource channel = channels[channelIndex];
        channel.transform.position = place;
        channel.clip = entry.clip;
        channel.volume = entry.volume;
        channel.pitch = 1f;
        channel.Play();
        channelSfx[channelIndex] = sfx;
    }

    /// <summary>
    /// AudioManager가 재생 중인 sfx만 정지합니다. (RandomFootstepPlayer 등
    /// AudioManager 밖에서 자체 AudioSource로 재생하는 소리는 대상이 아닙니다.)
    /// 같은 sfx가 여러 채널에서 동시에 재생 중이면 전부 정지합니다.
    /// Footstep처럼 계속 재생 중인 루프도 이 호출로 멈춥니다.
    /// </summary>
    public void StopSFX(SFX sfx)
    {
        if (null == channels)
        {
            return;
        }

        for (int i = 0; i < channels.Length; i++)
        {
            if (channelSfx[i] == sfx && (channels[i].isPlaying || channelLooping[i]))
            {
                StopChannel(i);
            }
        }
    }

    /// <summary>
    /// AudioManager가 재생 중인 모든 sfx를 정지합니다.
    /// </summary>
    public void StopAllSFX()
    {
        if (null == channels)
        {
            return;
        }

        for (int i = 0; i < channels.Length; i++)
        {
            StopChannel(i);
        }
    }

    private void StopChannel(int channelIndex)
    {
        if (channelLooping[channelIndex])
        {
            if (null != channelLoopRoutines[channelIndex])
            {
                StopCoroutine(channelLoopRoutines[channelIndex]);
                channelLoopRoutines[channelIndex] = null;
            }

            channelLooping[channelIndex] = false;
        }

        channels[channelIndex].Stop();
    }

    /// <summary>
    /// Footstep 전용 재생. StopSFX(SFX.Footstep)로 멈출 때까지 7종의 클립 중
    /// 직전과 다른 클립을 무작위로 골라 약간의 피치 변주를 주며 계속 이어 재생합니다.
    /// </summary>
    private void PlayFootstep(Vector3 place)
    {
        if (null == footstepClips || 0 == footstepClips.Length)
        {
            Debug.LogWarning("[AudioManager] Footstep에 등록된 클립이 없습니다.");
            return;
        }

        int channelIndex = GetFreeChannelIndex();
        channels[channelIndex].transform.position = place;
        channelSfx[channelIndex] = SFX.Footstep;
        channelLooping[channelIndex] = true;
        channelLoopRoutines[channelIndex] = StartCoroutine(FootstepLoop(channelIndex));
    }

    private IEnumerator FootstepLoop(int channelIndex)
    {
        AudioSource channel = channels[channelIndex];

        while (true)
        {
            AudioClip clip = PickFootstepClip();
            if (null == clip)
            {
                break;
            }

            channel.clip = clip;
            channel.volume = footstepVolume;
            channel.pitch = UnityEngine.Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            channel.Play();

            yield return new WaitForSeconds(clip.length / Mathf.Max(0.01f, channel.pitch));
        }

        channelLooping[channelIndex] = false;
        channelLoopRoutines[channelIndex] = null;
    }

    private AudioClip PickFootstepClip()
    {
        if (null == footstepClips || 0 == footstepClips.Length)
        {
            return null;
        }

        if (1 == footstepClips.Length)
        {
            return footstepClips[0];
        }

        int index = UnityEngine.Random.Range(0, footstepClips.Length);
        if (index == lastFootstepClipIndex)
        {
            index = (index + UnityEngine.Random.Range(1, footstepClips.Length)) % footstepClips.Length;
        }

        lastFootstepClipIndex = index;
        return footstepClips[index];
    }

    private void OnValidate()
    {
        if (footstepPitchRange.x > footstepPitchRange.y)
        {
            footstepPitchRange = new Vector2(footstepPitchRange.y, footstepPitchRange.x);
        }
    }

    private int GetFreeChannelIndex()
    {
        for (int i = 0; i < channels.Length; i++)
        {
            int index = (nextChannel + i) % channels.Length;
            if (!channels[index].isPlaying && !channelLooping[index])
            {
                nextChannel = (index + 1) % channels.Length;
                return index;
            }
        }

        // All channels are busy: steal the oldest one round-robin instead of dropping the request.
        int stolen = nextChannel;
        nextChannel = (nextChannel + 1) % channels.Length;

        if (channelLooping[stolen])
        {
            StopChannel(stolen);
        }

        return stolen;
    }
}
