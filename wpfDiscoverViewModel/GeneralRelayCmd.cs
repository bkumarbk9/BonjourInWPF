using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace wpfDiscoverViewModel
{
    /// <summary>
    /// General Relay command class for binding Vew events to Model actions
    /// </summary>
    class GeneralRelayCmd : ICommand
    {
        private readonly Action<object> action;

        public GeneralRelayCmd(Action<object> actionParam)
        {
            action = actionParam;
        }

        #region ICommand Members  
        public bool CanExecute(object parameter)
        {
            return true;
        }
        public event EventHandler CanExecuteChanged;
        public void Execute(object parameter)
        {
            action(parameter);
        }
        #endregion
    }
}
