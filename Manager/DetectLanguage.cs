using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace LanguageDetection
{
  public class Language
  {
    public bool Found = false;
    public bool Reliable = false;
    public string Code = "en";
  }

  public class DetectLanguage
  {
    public string dl_strAPIKey;
    public string dl_strURL = "http://" + "ws.detectlanguage.com/0.2/detect?q={0}&key={1}";

    public DetectLanguage(string strAPIKey)
    {
      dl_strAPIKey = strAPIKey;
    }

    public Language Detect(string strText)
    {
      WebClient wc = new WebClient();
      wc.Proxy = null;
      string strRequest = String.Format(dl_strURL, Uri.EscapeDataString(strText), dl_strAPIKey);
      dynamic res = JSON.JsonDecode(wc.DownloadString(strRequest));

      // if there are detections
      if (res["data"]["detections"].Count >= 1) {
        dynamic l = res["data"]["detections"][0];

        // construct a proper response
        Language ret = new Language();
        ret.Found = true;
        ret.Code = l["language"];
        ret.Reliable = l["isReliable"];

        // return language
        return ret;
      }

      return new Language();
    }
  }
}
