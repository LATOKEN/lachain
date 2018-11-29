namespace Phorkus.Hermes.Generator.Messages
{
    public class ComplaintMessage
    {
        /** The id of the party that produced the invalid share*/
        public int id;

        public ComplaintMessage(int id)
        {
            this.id = id;
        }
    }
}