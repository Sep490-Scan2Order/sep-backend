using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Enums
{
    public enum NoteWalletTransaction
    {
        // 1. Nạp tiền vào ví
        Deposit = 1,

        // 2. Mua gói Plan 
        PlanSubscription = 2,

        // 3. Mua gói Addon 
        AddonPurchase = 3,

        // 4. Nâng cấp gói 
        PlanUpgrade = 4,

        // 5. Tenant nhận tiền từ Voucher 
        VoucherPayout = 5,

        // 6. Admin bị trừ tiền Voucher 
        VoucherExpense = 6,

        // 7. Rút tiền về tài khoản ngân hàng
        Withdrawal = 7
    }
}
