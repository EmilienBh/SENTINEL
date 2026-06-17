
using UnityEngine;
using System.Collections;
using System;

//-----------------------------------------------------------------
// Waypoint
//-----------------------------------------------------------------

public class Waypoint : MonoBehaviour, IComparable {
    public static ArrayList waypoints = new ArrayList();

    public int id;

    public ArrayList targets;

    private Waypoint m_target;
    private int m_steps = 2;
    private float m_stepWidth = 5f;
    private float m_angle;
    private bool m_initilised = false;


    public static void reset() {
        waypoints = new ArrayList();
    }

    public static void InitAll() {
        Waypoint waypoint;

        waypoints.Sort(); // sort based on id

        for (int i = 0 ; i < waypoints.Count ; i++) {
            waypoint = (Waypoint) waypoints[i];
            waypoint.Init();
        }

    }

    public void Awake() {
        m_initilised = false;

        waypoints.Add(this);
    }

    public void Init() {
        targets = new ArrayList();

        int nextWaypoint;
        nextWaypoint = id + 1;

        if (nextWaypoint > waypoints.Count - 1) nextWaypoint = 0;

        m_target = (Waypoint) waypoints[nextWaypoint];

        //Debug.Log("Waypoint Added");

        float dx = m_target.transform.position.x - transform.position.x;
        float dz = m_target.transform.position.z - transform.position.z;

        m_angle = Mathf.Atan2(dz, dx) + Mathf.PI * 0.5f;

        // Build Subwaypoints

        dx = Mathf.Cos(m_angle);
        dz = Mathf.Sin(m_angle);

        for (int i = -m_steps ; i < m_steps ; i++) {
            Vector3 position = new Vector3(dx, 0f, dz) * i * m_stepWidth;
            targets.Add(position + transform.position);
        }

        m_initilised = true;


    }

    public int CompareTo(object obj) {
        Waypoint u = (Waypoint) obj;
        return this.id.CompareTo(u.id);
    }

    public void OnDrawGizmos() {


        Gizmos.color = Color.red;
        Gizmos.DrawCube(transform.position, Vector3.one * 5f);

        if (!m_initilised) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, m_target.transform.position);


        /*
        for (int j = 0; j < targets.Count; j++)
        {
            Vector3 position = (Vector3)targets[j];
            Gizmos.color = Color.white;
            Gizmos.DrawCube(position, Vector3.one);
        }
        */


        //Gizmos.DrawLine (transform.position - range, range + transform.position);
    }
}