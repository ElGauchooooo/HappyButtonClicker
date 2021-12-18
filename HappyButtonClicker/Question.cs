using System.Collections.Generic;
using System.Linq;

namespace HappyButtonClicker
{
    public class Question
    {
        public int Hash;
        public List<Answer> Answers = new List<Answer>();

        public string GetBestAnswer()
        {
            return Answers.First(x => x.Result == null).Text;
        }
    }
}