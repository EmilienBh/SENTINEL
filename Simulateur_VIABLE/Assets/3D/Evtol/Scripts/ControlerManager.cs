using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ControlerManager : MonoBehaviour
{
    public GameObject Elemt1;
    public GameObject Elemt2;
    public GameObject Elemt3;
    public GameObject Elemt4;
    public GameObject Elemt5;
    public GameObject Elemt6;
    public GameObject Elemt7;
    public GameObject Elemt8;
    public GameObject Elemt9;
    public GameObject Elemt10;
    public GameObject Elemt11;
    public GameObject Elemt12;
    public bool ElemtAff = true;
    public bool PorteOpen = false;
    public TextMeshProUGUI Myretour;
    public GameObject AnimationPorte;

    // Start is called before the first frame update
    void Start()
    {
        Myretour.text = "";
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            if (ElemtAff == true)
            {
                ElemtAff = false;
            }
            else
            {
                ElemtAff = true;
            }
            Myretour.text = ElemtAff.ToString();
            Elemt1.SetActive(ElemtAff);
            Elemt2.SetActive(ElemtAff);
            Elemt3.SetActive(ElemtAff);
            Elemt4.SetActive(ElemtAff);
            Elemt5.SetActive(ElemtAff);
            Elemt6.SetActive(ElemtAff);
            Elemt7.SetActive(ElemtAff);
            Elemt8.SetActive(ElemtAff);
            Elemt9.SetActive(ElemtAff);
            Elemt10.SetActive(ElemtAff);
            Elemt11.SetActive(ElemtAff);
            Elemt12.SetActive(ElemtAff);
        }

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            if (PorteOpen == false)
            {
                AnimationPorte.GetComponent<Animator>().SetBool("PlayPorte", true);
                PorteOpen = true;
            }
            else
            {
                AnimationPorte.GetComponent<Animator>().SetBool("PlayPorte", false);
                PorteOpen = false;
            }

        }
    }
}
