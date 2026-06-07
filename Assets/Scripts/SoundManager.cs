using UnityEngine;
using AK.Wwise;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AK.Wwise.Event playAmbience;
    [SerializeField] private AK.Wwise.Event playBike;
    [SerializeField] private AK.Wwise.Event playWind;
    [SerializeField] private AK.Wwise.RTPC bikeSpeedRTPC;
    [SerializeField] private AK.Wwise.RTPC windSideRTPC;

    private GameObject player;
    private float windPhase;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    private void Start()
    {
        player = GameManager.Instance.bikeController.gameObject;
        playAmbience.Post(player);
        playBike.Post(player);
        playWind.Post(player);
    }

    private void Update()
    {
        float bikeSpeed = GameManager.Instance.GetActiveBikeController()?.CurrentSpeed ?? 0f;
        float speed01 = Mathf.Clamp01(bikeSpeed / 25f);
        bikeSpeedRTPC.SetValue(player, speed01 * 100f);
        float oscillationFrequency = Mathf.Lerp(
                0.5f,
                8f,
                speed01
            );
        windPhase += Time.deltaTime * oscillationFrequency * Mathf.PI * 2f;
        float amplitude = speed01 * 100f;
        float stereoPan = Mathf.Sin(windPhase) * amplitude;
        windSideRTPC.SetValue(player, stereoPan / 2f + 50f);
    }
}