using System;

namespace GRushSdk
{
    public interface IGRushLocaleBackend
    {
        string LocaleCurrentJson();
        void SetLocaleChangedHandler(Action<string> handler);
    }

    // 表示言語の変更の購読を、バックエンドの差し替えをまたいで続ける。
    // バックエンドは通知をその時点のハンドラごと GRushDispatcher に積むので、差し替えの前に積まれた通知が
    // 差し替えの後に届く。登録したときの世代を閉じ込め、今の世代と違う通知は捨てる。
    // UnityEngine に依存させないので、テストからこのファイルだけを取り出してコンパイルできる。
    internal sealed class GRushLocaleSubscription
    {
        private readonly Action<string> deliver;
        private int generation;
        private bool subscribed;

        public GRushLocaleSubscription(Action<string> deliver)
        {
            this.deliver = deliver;
        }

        public bool Subscribed
        {
            get { return subscribed; }
        }

        // backend は表示言語を使えないときに null。
        public void Subscribe(IGRushLocaleBackend backend, bool hasListeners)
        {
            if (subscribed || !hasListeners || backend == null)
            {
                return;
            }
            subscribed = true;
            var registered = generation;
            backend.SetLocaleChangedHandler(json =>
            {
                if (registered == generation)
                {
                    deliver(json);
                }
            });
        }

        // バックエンドを差し替えた後に呼ぶ。前の世代のハンドラから届く通知を捨て、新しいバックエンドへつなぎ直す。
        public void Rebind(IGRushLocaleBackend backend, bool hasListeners)
        {
            generation++;
            subscribed = false;
            Subscribe(backend, hasListeners);
        }
    }
}
