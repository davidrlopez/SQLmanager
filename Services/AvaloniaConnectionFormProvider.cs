
namespace SQLmanager.Services
{
    public class AvaloniaConnectionFormProvider : IConnectionFormProvider
    {
        public ConnectionForm Form;

        public AvaloniaConnectionFormProvider(ConnectionForm form)
        {
            Form = form;
        }

        public ConnectionForm GetConnectionForm()
        {
            return Form;
        }
    }
}
