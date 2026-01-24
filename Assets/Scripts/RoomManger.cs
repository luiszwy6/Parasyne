using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Rooms")]
    [SerializeField] private Room[] rooms;
    [SerializeField] private Room defaultRoom;

    public Room Current { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (rooms != null)
        {
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null) rooms[i].id = i;
        }
    }

    private void Start()
    {
        // Hide all rooms at start
        if (rooms != null)
        {
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null) rooms[i].SetVisible(false);
        }

        if (defaultRoom == null && rooms != null && rooms.Length > 0)
            defaultRoom = rooms[0];

        if (defaultRoom != null)
            SwitchTo(defaultRoom);
    }

    public void SwitchTo(Room next)
    {
        if (next == null) return;
        if (Current == next) return;

        if (Current != null)
            Current.SetVisible(false);

        Current = next;
        Current.SetVisible(true);
    }

    public Room[] GetRooms() => rooms;
}
