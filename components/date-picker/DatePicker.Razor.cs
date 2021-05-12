using System;
using System.Globalization;
using System.Threading.Tasks;
using AntDesign.Core.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AntDesign
{
    public partial class DatePicker<TValue> : DatePickerBase<TValue>
    {
        private TValue _cacheDuringInput;

        private DateTime _pickerValuesAfterInit;

        [Parameter] public EventCallback<DateTimeChangedEventArgs> OnChange { get; set; }

        public override void ChangeValue(DateTime value, int index = 0)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException("DatePicker should have only single picker.");
            }

            UseDefaultPickerValue[0] = false;
            bool result = BindConverter.TryConvertTo<TValue>(
                value.ToString(CultureInfo), CultureInfo, out var dateTime);

            if (result)
            {
                CurrentValue = dateTime;
            }

            _pickerStatus[0]._hadSelectValue = true;

            UpdateCurrentValueAsString();

            if (!IsShowTime && Picker != DatePickerType.Time)
            {
                Close();
            }

            if (OnChange.HasDelegate)
            {
                OnChange.InvokeAsync(new DateTimeChangedEventArgs {Date = value, DateString = GetInputValue(0)});
            }
        }

        public override void ClearValue(int index = 0, bool closeDropdown = true)
        {
            _isSetPicker = false;

            if (!IsNullable && DefaultValue != null)
                CurrentValue = DefaultValue;
            else
                CurrentValue = default;
            if (closeDropdown)
                Close();
        }

        /// <summary>
        /// Get value of the picker
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public override DateTime? GetIndexValue(int index = 0)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException("DatePicker should have only single picker.");
            }

            if (_pickerStatus[0]._hadSelectValue)
            {
                if (Value == null)
                {
                    return null;
                }

                return Convert.ToDateTime(Value, CultureInfo);
            }
            else if (DefaultValue != null)
            {
                return Convert.ToDateTime(DefaultValue, CultureInfo);
            }

            return null;
        }

        protected override Task OnBlur(int index)
        {
            if (_openingOverlay)
                return Task.CompletedTask;

            if (_duringManualInput)
            {
                if (!Value.Equals(_cacheDuringInput))
                {
                    //reset picker to Value         
                    Value = _cacheDuringInput;
                    _pickerStatus[0]._hadSelectValue =
                        !(Value is null && (DefaultValue is not null || DefaultPickerValue is not null));
                    GetIfNotNull(Value ?? DefaultValue ?? DefaultPickerValue, (notNullValue) =>
                    {
                        PickerValues[0] = notNullValue;
                    });
                }

                _duringManualInput = false;
            }

            AutoFocus = false;
            return Task.CompletedTask;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            ProcessDefaults();
            _pickerValuesAfterInit = PickerValues[0];
        }

        protected void OnInput(ChangeEventArgs args, int index = 0)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException("DatePicker should have only single picker.");
            }

            if (args == null)
            {
                return;
            }

            if (!_duringManualInput)
            {
                _duringManualInput = true;
                _cacheDuringInput = Value;
            }

            var val = args.Value.ToString();
            if (args.Value.ToString().Length == 8 && this.Picker == DatePickerType.Date)
            {
                if (DateTime.TryParseExact(args.Value.ToString(), "yyyyMMdd",
                    CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces,
                    out var dt))
                {
                    if (typeof(TValue) == typeof(DateTime) || typeof(TValue) == typeof(DateTime?))
                    {
                        var ret = OnSelect(dt).AsyncState;
                    }

                    if (typeof(TValue) == typeof(string))
                    {
                        var ret = OnSelect(dt).AsyncState;
                    }
                }
            }
            else if (args.Value.ToString().Length == 6 && this.Picker == DatePickerType.Month)
            {
                if (DateTime.TryParseExact(args.Value.ToString() + "01", "yyyyMMdd",
                    CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces,
                    out var dt))
                {
                    if (typeof(TValue) == typeof(DateTime) || typeof(TValue) == typeof(DateTime?))
                    {
                        var ret = OnSelect(dt).AsyncState;
                    } 
                }
            }
            else if (FormatAnalyzer.TryPickerStringConvert(val, out TValue changeValue, IsNullable))
            {
                Value = changeValue;
                GetIfNotNull(changeValue, (notNullValue) =>
                {
                    PickerValues[0] = notNullValue;
                });

                StateHasChanged();
            }

            UpdateCurrentValueAsString();
        }

        /// <summary>
        /// Method is called via EventCallBack if the keyboard key is no longer pressed inside the Input element.
        /// </summary>
        /// <param name="e">Contains the key (combination) which was pressed inside the Input element</param>
        protected async Task OnKeyDown(KeyboardEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var key = e.Key.ToUpperInvariant();
            if (key == "ENTER" || key == "TAB" || key == "ESCAPE")
            {
                _duringManualInput = false;

                if (string.IsNullOrWhiteSpace(_inputStart.Value))
                    ClearValue();
                else
                    await TryApplyInputValue();

                if (key == "ESCAPE" && _dropDown.IsOverlayShow())
                {
                    Close();
                    await Js.FocusAsync(_inputStart.Ref);
                }

                if (key == "ENTER")
                {
                    //needed only in wasm, details: https://github.com/dotnet/aspnetcore/issues/30070
                    await Task.Yield();
                    await Js.InvokeVoidAsync(JSInteropConstants.InvokeTabKey);
                }

                Close();
                AutoFocus = false;
                return;
            }

            if (key == "ARROWDOWN" && !_dropDown.IsOverlayShow())
            {
                await _dropDown.Show();
                return;
            }

            if (key == "ARROWUP" && _dropDown.IsOverlayShow())
            {
                Close();
                return;
            }
        }

        protected override void OnValueChange(TValue value)
        {
            base.OnValueChange(value);
            _pickerStatus[0]._hadSelectValue = true;
        }

        private void GetIfNotNull(TValue value, Action<DateTime> notNullAction)
        {
            if (!IsNullable)
            {
                DateTime dateTime = Convert.ToDateTime(value, CultureInfo);
                if (dateTime != DateTime.MinValue)
                {
                    notNullAction?.Invoke(dateTime);
                }
            }

            if (IsNullable && value != null)
            {
                notNullAction?.Invoke(Convert.ToDateTime(value, CultureInfo));
            }
        }

        private async Task OnInputClick()
        {
            if (_duringManualInput)
            {
                return;
            }

            _openingOverlay = !_dropDown.IsOverlayShow();

            AutoFocus = true;
            //Reset Picker to default in case it the picker value was changed
            //but no value was selected (for example when a user clicks next 
            //month but does not select any value)
            if (UseDefaultPickerValue[0] && DefaultPickerValue != null)
            {
                PickerValues[0] = _pickerValuesAfterInit;
            }

            await _dropDown.Show();

            // clear status
            _pickerStatus[0]._currentShowHadSelectValue = false;

            if (!_inputStart.IsOnFocused && _pickerStatus[0]._hadSelectValue && !UseDefaultPickerValue[0])
            {
                GetIfNotNull(Value, notNullValue =>
                {
                    ChangePickerValue(notNullValue);
                });
            }
        }

        private void OverlayVisibleChange(bool visible)
        {
            OnOpenChange.InvokeAsync(visible);
            _openingOverlay = false;
        }

        private void ProcessDefaults()
        {
            UseDefaultPickerValue[0] = true;
            if (DefaultPickerValue.Equals(default(TValue)))
            {
                if ((IsNullable && Value != null) || (!IsNullable && !Value.Equals(default(TValue))))
                {
                    DefaultPickerValue = Value;
                }
                else if ((IsNullable && DefaultValue != null) || (!IsNullable && !DefaultValue.Equals(default(TValue))))
                {
                    DefaultPickerValue = DefaultValue;
                }
                else if (!IsNullable && Value.Equals(default(TValue)))
                {
                    DefaultPickerValue = Value;
                }
                else
                {
                    UseDefaultPickerValue[0] = false;
                }
            }

            if (UseDefaultPickerValue[0])
            {
                PickerValues[0] = Convert.ToDateTime(DefaultPickerValue, CultureInfo);
            }
        }

        private async Task TryApplyInputValue()
        {
            if (FormatAnalyzer.TryPickerStringConvert(_inputStart.Value, out TValue changeValue, IsNullable))
            {
                if (CurrentValue.Equals(changeValue)) return;
                CurrentValue = changeValue;
                GetIfNotNull(changeValue, (notNullValue) =>
                {
                    PickerValues[0] = notNullValue;
                });
                if (OnChange.HasDelegate)
                {
                    await OnChange.InvokeAsync(new DateTimeChangedEventArgs
                    {
                        Date = Convert.ToDateTime(changeValue, this.CultureInfo), DateString = GetInputValue(0)
                    });
                }
            }
        }
    }
}
