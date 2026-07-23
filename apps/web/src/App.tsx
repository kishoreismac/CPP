import { useEffect, useRef, useState } from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  Link,
  useNavigate,
  useParams,
  useLocation,
} from "react-router-dom";
import {
  QueryClient,
  QueryClientProvider,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Search,
  Download,
  Plus,
  X,
  Star,
  MessageCircle,
  ChevronDown,
  ChevronRight,
  HelpCircle,
  Package,
  CheckCircle2,
  AlertTriangle,
  Printer,
  Minus,
  ShoppingCart,
  Pencil,
  Truck,
  FileText,
  RotateCcw,
} from "lucide-react";
import {
  api,
  API,
  type Account,
  type AssistantMissingField,
  type AssistantOrderLine,
  type AssistantRequest,
  type AssistantResponse,
  type AssistantToolCall,
  type DeliverTo,
  type AlternateDelivery,
  type Product,
  type Order,
  type Line,
  type Validation,
} from "./api";
import "./styles.css";
const qc = new QueryClient({ defaultOptions: { queries: { retry: 1 } } });
const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});
const future = () => {
  const d = new Date();
  d.setDate(d.getDate() + 7);
  return d.toISOString().slice(0, 10);
};
const assistantStarters = [
  {
    label: "Find by active ingredient",
    detail: "Choose from all active ingredients",
    mode: "activeIngredient" as const,
    kind: "search",
  },
  {
    label: "Find by product name",
    detail: "Choose from the complete product catalog",
    mode: "productName" as const,
    kind: "search",
  },
  {
    label: "Draft a Gallatin order",
    detail: "CORNERSTONE 5 PLUS · 10 units",
    prompt:
      "Create a draft order for 10 units of CORNERSTONE 5 PLUS using the MFA-GALLATIN account, delivered to Gallatin Bulk Facility. Let me review it before submission.",
    kind: "order",
  },
  {
    label: "Draft a Boonville order",
    detail: "AZOXYSTROBIN 2SC · 20 units · PO included",
    prompt:
      "Create a draft order for 20 units of AZOXYSTROBIN 2SC using the MFA-BOONVILLE account, delivered to Boonville Bulk Facility, with customer PO 595468768. Let me review it before submission.",
    kind: "order",
  },
];
const assistantWelcomeMessage =
  "I can find products, prepare CPP orders, and keep the order ready for your review before submission.";
function App() {
  return (
    <QueryClientProvider client={qc}>
      <BrowserRouter>
        <Shell>
          <Routes>
            <Route path="/" element={<Navigate to="/command-center" />} />
            <Route path="/command-center" element={<CommandCenter />} />
            <Route
              path="/orders/general"
              element={<OrderHistoryPage title="General Orders" />}
            />
            <Route
              path="/orders/history"
              element={<OrderHistoryPage title="Order History" />}
            />
            <Route path="/accounts" element={<AccountsPage />} />
            <Route path="/accounts/new" element={<AccountEditor />} />
            <Route path="/accounts/:accountId" element={<AccountEditor />} />
            <Route path="/crop-protection/orders" element={<Orders />} />
            <Route
              path="/crop-protection/orders/new"
              element={<OrderEditor />}
            />
            <Route
              path="/crop-protection/orders/:orderId/edit"
              element={<OrderEditor />}
            />
            <Route
              path="/crop-protection/orders/:orderId/review"
              element={<Review />}
            />
            <Route
              path="/crop-protection/orders/:orderId/confirmation"
              element={<Confirmation />}
            />
            <Route
              path="/crop-protection/orders/:orderId"
              element={<Review readOnly />}
            />
            <Route path="/section/:sectionName" element={<Section />} />
          </Routes>
        </Shell>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
function Shell({ children }: { children: React.ReactNode }) {
  const [menu, setMenu] = useState<"orders" | "cpp" | null>(null);
  const location = useLocation();
  const cpp = location.pathname.startsWith("/crop-protection");
  function key(e: React.KeyboardEvent) {
    if (e.key === "Escape") setMenu(null);
    if (e.key === "ArrowRight") setMenu(menu === "orders" ? "cpp" : "orders");
  }
  return (
    <>
      <header onKeyDown={key}>
        <div className="env">QA 1</div>
        <Link className="brand" to="/command-center">
          EVOLVE
        </Link>
        <nav aria-label="Primary">
          <button
            aria-expanded={menu === "orders"}
            onClick={() => setMenu(menu === "orders" ? null : "orders")}
            className="navbtn"
          >
            Orders <ChevronDown size={14} />
          </button>
          <Link to="/section/Seed">Seed</Link>
          <button
            data-testid="crop-protection-menu"
            aria-expanded={menu === "cpp"}
            onMouseEnter={() => setMenu("cpp")}
            onClick={() => setMenu(menu === "cpp" ? null : "cpp")}
            className={`navbtn ${cpp ? "nav-active" : ""}`}
          >
            Crop Protection <ChevronDown size={14} />
          </button>
          <Link to="/accounts">Account</Link>
          {["Emulation", "Prepay", "Partner Websites", "Programs"].map((x) => (
            <Link key={x} to={`/section/${encodeURIComponent(x)}`}>
              {x}
            </Link>
          ))}
        </nav>
        <div className="context">
          <span>
            Company Code: <b>WinField United</b>
          </span>
          <span>
            Year: <b>2026</b>
          </span>
          <HelpCircle size={18} />
          <details>
            <summary>RVERVE13 · Administrator</summary>
            <div className="user-menu">
              <b>Development role switcher</b>
              {[
                "Administrator",
                "Retailer",
                "Sales Representative",
                "Support",
              ].map((x) => (
                <button key={x}>{x}</button>
              ))}
            </div>
          </details>
        </div>
        {menu === "orders" && (
          <div className="order-menu" onMouseLeave={() => setMenu(null)}>
            <b>Orders</b>
            <Link onClick={() => setMenu(null)} to="/orders/general">
              General Orders
            </Link>
            <Link onClick={() => setMenu(null)} to="/orders/history">
              Order History
            </Link>
          </div>
        )}
        {menu === "cpp" && (
          <div
            className="order-menu cpp-menu"
            onMouseLeave={() => setMenu(null)}
          >
            <b>CPP Orders</b>
            <div className="submenu">
              <Link
                data-testid="cpp-order-link"
                onClick={() => setMenu(null)}
                to="/crop-protection/orders"
              >
                CPP Order
              </Link>
              <Link
                onClick={() => setMenu(null)}
                to="/section/SRA Summer Fill Order"
              >
                SRA Summer Fill Order
              </Link>
            </div>
            {[
              "CPP Delivery Instructions & Contact List",
              "Error Order Maintenance",
              "CPP/PNP Pricing",
              "CPP Returns",
              "COI Fulfillment",
              "Order Product Labels",
              "Marketing Program",
            ].map((x) => (
              <Link
                onClick={() => setMenu(null)}
                key={x}
                to={`/section/${encodeURIComponent(x)}`}
              >
                {x}
              </Link>
            ))}
          </div>
        )}
      </header>
      <main>{children}</main>
      <Assistant />
    </>
  );
}
function PageTitle({
  children,
  actions,
}: {
  children: React.ReactNode;
  actions?: React.ReactNode;
}) {
  return (
    <div className="titlebar">
      <h1>{children}</h1>
      <div>{actions}</div>
    </div>
  );
}
function CommandCenter() {
  const { data: messages = [] } = useQuery<any[]>({
    queryKey: ["messages"],
    queryFn: () => api("/dashboard/messages"),
  });
  const { data: knowledge = [] } = useQuery<any[]>({
    queryKey: ["knowledge"],
    queryFn: () => api("/dashboard/knowledge"),
  });
  const { data: orders = [] } = useQuery<any[]>({
    queryKey: ["dashboard-orders"],
    queryFn: () => api("/orders"),
  });
  const [term, setTerm] = useState("");
  const [pins, setPins] = useState<number[]>([]);
  return (
    <>
      <div className="command">
        <h1>EVOLVE COMMAND CENTER</h1>
        <time>{new Date().toLocaleString()}</time>
      </div>
      <div className="dashboard">
        <aside>
          <section className="card">
            <h2>Message Center</h2>
            {messages.map((m) => (
              <details key={m.id}>
                <summary>
                  <button
                    aria-label="Pin message"
                    onClick={(e) => {
                      e.preventDefault();
                      setPins((p) =>
                        p.includes(m.id)
                          ? p.filter((x) => x !== m.id)
                          : [...p, m.id],
                      );
                    }}
                  >
                    {pins.includes(m.id) ? "●" : "○"}
                  </button>{" "}
                  {m.title}
                </summary>
                <p>{m.snippet}</p>
                <small>{new Date(m.timestamp).toLocaleString()}</small>
              </details>
            ))}
            <button className="text">View All</button>
          </section>
          <section className="card">
            <h2>Knowledge Center</h2>
            <div className="inline">
              <input
                aria-label="Knowledge topic"
                value={term}
                onChange={(e) => setTerm(e.target.value)}
                placeholder="Search topics"
              />
              <button>
                <Search size={16} />
              </button>
            </div>
            {knowledge
              .filter((k) => k.title.toLowerCase().includes(term.toLowerCase()))
              .map((k) => (
                <p key={k.id}>◉ {k.title}</p>
              ))}
            <button className="text">View All</button>
          </section>
        </aside>
        <section className="card grow">
          <div className="tabs">
            <button className="active">Crop Protection Orders</button>
            <button>Seed Orders</button>
          </div>
          <div className="toolbar">
            <span>View in-transit orders</span>
            <select aria-label="Date range">
              <option>Last 90 days</option>
            </select>
          </div>
          <Table>
            <thead>
              <tr>
                {[
                  "Web Order Number",
                  "Order Number",
                  "Product Name",
                  "Vendor",
                  "Ship-To Location",
                  "Shipped Qty",
                  "UOM",
                  "Status",
                  "Tracking",
                ].map((x) => (
                  <th key={x}>{x}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => (
                <tr key={o.id}>
                  <td>{o.webOrderNumber ?? "—"}</td>
                  <td>{o.id}</td>
                  <td>
                    {o.lines?.length
                      ? "Selected products"
                      : "CPP product order"}
                  </td>
                  <td>WinField United</td>
                  <td>{o.account?.name ?? o.shipToAccountId}</td>
                  <td>
                    {o.lines?.reduce(
                      (n: number, l: Line) => n + l.quantity,
                      0,
                    ) ?? 0}
                  </td>
                  <td>CS</td>
                  <td>
                    <Status value={o.status} />
                  </td>
                  <td>{o.status === "Submitted" ? "In transit" : "—"}</td>
                </tr>
              ))}
            </tbody>
          </Table>
          <div className="summary">Shipped 1 · In-transit 1 · Returned 0</div>
        </section>
      </div>
    </>
  );
}
function Orders() {
  const [tab, setTab] = useState("Submitted");
  const [q, setQ] = useState("");
  const [size, setSize] = useState(10);
  const {
    data = [],
    isLoading,
    isError,
    refetch,
  } = useQuery<any[]>({
    queryKey: ["orders", tab],
    queryFn: () => api(`/orders?status=${tab}`),
  });
  const rows = data
    .filter(
      (o) => !q || JSON.stringify(o).toLowerCase().includes(q.toLowerCase()),
    )
    .slice(0, size);
  return (
    <>
      <PageTitle
        actions={
          <Link className="button primary" to="/crop-protection/orders/new">
            <Plus size={17} /> Create New Order
          </Link>
        }
      >
        Crop Protection Orders
      </PageTitle>
      <section className="panel">
        <div className="searchbar">
          <select aria-label="Order search field">
            <option>Account Name</option>
            <option>Account Number</option>
            <option>Order ID</option>
            <option>Customer PO Number</option>
            <option>Status</option>
          </select>
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search orders"
          />
          <button className="primary">
            <Search size={16} /> Search
          </button>
          <a className="button" href={`${API}/orders/export`}>
            <Download size={16} /> Export
          </a>
        </div>
        <div className="tabs">
          {[
            ["Submitted", "Placed Orders"],
            ["Draft", "Draft Orders"],
            ["ReadyForReview", "Early Advantage Orders"],
            ["New", "Early Advantage Draft Orders"],
          ].map(([v, l]) => (
            <button
              key={v}
              onClick={() => setTab(v)}
              className={tab === v ? "active" : ""}
            >
              {l}
            </button>
          ))}
        </div>
        {isLoading ? (
          <Skeleton />
        ) : isError ? (
          <Empty
            text="Orders could not be loaded."
            action={<button onClick={() => refetch()}>Retry</button>}
          />
        ) : rows.length === 0 ? (
          <Empty
            text={`No ${tab.toLowerCase()} orders match your current filters.`}
          />
        ) : (
          <Table>
            <thead>
              <tr>
                <th>Order ID</th>
                <th>Customer PO #</th>
                <th>Account Name</th>
                <th>Account #</th>
                <th>Order Date</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((o) => (
                <tr key={o.id}>
                  <td>
                    <b>{o.webOrderNumber ?? o.id}</b>
                  </td>
                  <td>{o.customerPo ?? "—"}</td>
                  <td>
                    {o.account?.name}
                    <small>{o.account?.address}</small>
                  </td>
                  <td>{o.account?.accountNumber}</td>
                  <td>{new Date(o.updatedAt).toLocaleDateString()}</td>
                  <td>
                    <Status value={o.status} />
                  </td>
                  <td>
                    <Link
                      to={`/crop-protection/orders/${o.id}${o.status === "Draft" || o.status === "SubmissionFailed" ? "/edit" : ""}`}
                    >
                      {o.status === "Draft" ? "Edit" : "View"}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
        <div className="pager">
          Items per page{" "}
          <select value={size} onChange={(e) => setSize(+e.target.value)}>
            <option>10</option>
            <option>25</option>
            <option>50</option>
          </select>
          <span>
            1–{rows.length} of {data.length}
          </span>
        </div>
      </section>
    </>
  );
}
type HistoryOrder = {
  id: string;
  webOrderNumber?: string;
  status: string;
  customerPo?: string;
  createdAt: string;
  updatedAt: string;
  submittedAt?: string;
  account?: Account;
  products: {
    productId: string;
    name: string;
    quantity: number;
    uom: string;
    unitPrice: number;
    total: number;
  }[];
  totalQuantity: number;
  totalAmount: number;
  canEdit: boolean;
};
function OrderHistoryPage({ title }: { title: string }) {
  const [q, setQ] = useState("");
  const {
    data = [],
    isLoading,
    isError,
    refetch,
  } = useQuery<HistoryOrder[]>({
    queryKey: ["complete-order-history"],
    queryFn: () => api("/orders"),
  });
  const normalized = q.trim().toLowerCase();
  const rows = data.filter(
    (order) =>
      !normalized ||
      `${order.id} ${order.webOrderNumber ?? ""} ${order.customerPo ?? ""} ${order.account?.name ?? ""} ${order.status} ${order.products.map((p) => p.name).join(" ")}`
        .toLowerCase()
        .includes(normalized),
  );
  return (
    <>
      <PageTitle
        actions={
          <Link className="button primary" to="/crop-protection/orders/new">
            <Plus size={17} /> Create New Order
          </Link>
        }
      >
        {title}
      </PageTitle>
      <section className="panel">
        <div className="searchbar">
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search order, account, PO, status, or product"
            aria-label="Search complete order history"
          />
          <button className="primary">
            <Search size={16} /> Search
          </button>
          <a className="button" href={`${API}/orders/export`}>
            <Download size={16} /> Export
          </a>
        </div>
        {isLoading ? (
          <Skeleton />
        ) : isError ? (
          <Empty
            text="Order history could not be loaded."
            action={<button onClick={() => refetch()}>Retry</button>}
          />
        ) : rows.length === 0 ? (
          <Empty text="No orders match your search." />
        ) : (
          <Table>
            <thead>
              <tr>
                <th>Order Number</th>
                <th>Date</th>
                <th>Account</th>
                <th>Products</th>
                <th>Quantity</th>
                <th>Total Amount</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((order) => (
                <tr key={order.id}>
                  <td>
                    <b>{order.webOrderNumber ?? order.id}</b>
                    {order.webOrderNumber && <small>{order.id}</small>}
                  </td>
                  <td>
                    {new Date(
                      order.submittedAt ?? order.updatedAt,
                    ).toLocaleDateString()}
                  </td>
                  <td>
                    {order.account?.name ?? "Unknown account"}
                    <small>{order.account?.accountNumber}</small>
                  </td>
                  <td>
                    {order.products.length
                      ? order.products.map((product) => (
                          <small key={product.productId}>
                            {product.name} — {product.quantity} {product.uom}
                          </small>
                        ))
                      : "No product lines"}
                  </td>
                  <td>{order.totalQuantity}</td>
                  <td>{money.format(order.totalAmount)}</td>
                  <td>
                    <Status value={order.status} />
                  </td>
                  <td>
                    <Link
                      to={
                        order.canEdit
                          ? `/crop-protection/orders/${order.id}/edit`
                          : `/crop-protection/orders/${order.id}`
                      }
                    >
                      {order.canEdit ? "Edit" : "View"}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
        <p className="notice">
          Submitted, submitting, and cancelled orders are read-only. Drafts and
          recoverable failed orders remain editable.
        </p>
      </section>
    </>
  );
}
type AccountDraft = Omit<Account, "id">;
const blankAccount: AccountDraft = {
  accountNumber: "",
  name: "",
  address: "",
  city: "",
  state: "MO",
  postalCode: "",
  soldToName: "",
  contactEmail: "",
  shippingInstructions: "",
  requiresCustomerPo: false,
};
function AccountsPage() {
  const [q, setQ] = useState("");
  const {
    data = [],
    isLoading,
    isError,
    refetch,
  } = useQuery<Account[]>({
    queryKey: ["accounts"],
    queryFn: () => api("/accounts"),
  });
  const rows = data.filter((account) =>
    `${account.name} ${account.accountNumber} ${account.city} ${account.state} ${account.postalCode}`
      .toLowerCase()
      .includes(q.trim().toLowerCase()),
  );
  return (
    <>
      <PageTitle
        actions={
          <Link className="button primary" to="/accounts/new">
            <Plus size={17} /> Create Account
          </Link>
        }
      >
        Customer Accounts
      </PageTitle>
      <section className="panel">
        <div className="searchbar">
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search name, account number, city, state, or postal code"
            aria-label="Search accounts"
          />
          <button className="primary">
            <Search size={16} /> Search
          </button>
        </div>
        {isLoading ? (
          <Skeleton />
        ) : isError ? (
          <Empty
            text="Accounts could not be loaded."
            action={<button onClick={() => refetch()}>Retry</button>}
          />
        ) : rows.length === 0 ? (
          <Empty text="No customer accounts match your search." />
        ) : (
          <Table>
            <thead>
              <tr>
                <th>Account</th>
                <th>Sold To</th>
                <th>Address</th>
                <th>Contact</th>
                <th>PO Requirement</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((account) => (
                <tr key={account.id}>
                  <td>
                    <b>{account.name}</b>
                    <small>{account.accountNumber}</small>
                  </td>
                  <td>{account.soldToName}</td>
                  <td>
                    {account.address}
                    <small>
                      {account.city}, {account.state} {account.postalCode}
                    </small>
                  </td>
                  <td>{account.contactEmail || "Not supplied"}</td>
                  <td>
                    {account.requiresCustomerPo ? "Required" : "Optional"}
                  </td>
                  <td>
                    <Link to={`/accounts/${account.id}`}>View / Edit</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </section>
    </>
  );
}
function AccountEditor() {
  const { accountId } = useParams();
  const nav = useNavigate();
  const query = useQueryClient();
  const [draft, setDraft] = useState<AccountDraft>(blankAccount);
  const [attempted, setAttempted] = useState(false);
  const existing = useQuery<Account>({
    queryKey: ["account", accountId],
    queryFn: () => api(`/accounts/${accountId}`),
    enabled: !!accountId,
  });
  useEffect(() => {
    if (existing.data) {
      const { id: _id, ...account } = existing.data;
      void _id;
      setDraft(account);
    }
  }, [existing.data]);
  const valid =
    !!draft.accountNumber.trim() &&
    !!draft.name.trim() &&
    !!draft.address.trim() &&
    !!draft.city.trim() &&
    !!draft.state.trim() &&
    !!draft.postalCode.trim() &&
    !!draft.soldToName.trim() &&
    (!draft.contactEmail || draft.contactEmail.includes("@"));
  const save = useMutation({
    mutationFn: () =>
      api<Account>(accountId ? `/accounts/${accountId}` : "/accounts", {
        method: accountId ? "PUT" : "POST",
        body: JSON.stringify(draft),
      }),
    onSuccess: (account) => {
      query.invalidateQueries({ queryKey: ["accounts"] });
      query.setQueryData(["account", account.id], account);
      nav("/accounts");
    },
  });
  if (accountId && existing.isLoading) return <Skeleton />;
  return (
    <>
      <PageTitle>
        {accountId ? "View / Edit Account" : "Create Account"}
      </PageTitle>
      <section className="panel form">
        <div className="grid">
          {(
            [
              ["accountNumber", "Account Number *"],
              ["name", "Account Name *"],
              ["soldToName", "Sold-To Name *"],
              ["address", "Street Address *"],
              ["city", "City *"],
              ["state", "State *"],
              ["postalCode", "Postal Code *"],
              ["contactEmail", "Contact Email"],
            ] as [keyof AccountDraft, string][]
          ).map(([key, label]) => (
            <label key={key}>
              {label}
              <input
                value={String(draft[key])}
                onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}
              />
            </label>
          ))}
          <label className="wide">
            Shipping Instructions
            <textarea
              value={draft.shippingInstructions}
              onChange={(e) =>
                setDraft({ ...draft, shippingInstructions: e.target.value })
              }
            />
          </label>
          <label className="check">
            <input
              type="checkbox"
              checked={draft.requiresCustomerPo}
              onChange={(e) =>
                setDraft({ ...draft, requiresCustomerPo: e.target.checked })
              }
            />{" "}
            Customer PO is required
          </label>
        </div>
        {attempted && !valid && (
          <p className="error" role="alert">
            Complete all required fields and enter a valid contact email.
          </p>
        )}
        {save.isError && (
          <p className="error" role="alert">
            {save.error.message}
          </p>
        )}
        <div className="footer">
          <button onClick={() => nav("/accounts")}>Cancel</button>
          <button
            className="primary"
            disabled={save.isPending}
            onClick={() => {
              setAttempted(true);
              if (valid) save.mutate();
            }}
          >
            {accountId ? "Save Changes" : "Create Account"}
          </button>
        </div>
      </section>
    </>
  );
}
type Draft = {
  id?: string;
  shipToAccountId: string;
  deliverToAccountId?: string;
  deliverToAnotherLocation: boolean;
  alternateDelivery: AlternateDelivery;
  customerPo: string;
  contactEmail: string;
  shippingInstructions: string;
  customerPickup: boolean;
  freightOption: string;
  requestedArrivalDate: string;
  lines: Line[];
};
const emptyAlternate: AlternateDelivery = {
  locationName: "",
  addressLine1: "",
  addressLine2: "",
  city: "",
  state: "MO",
  postalCode: "",
  contactName: "",
  contactPhone: "",
};
const blank: Draft = {
  shipToAccountId: "",
  deliverToAnotherLocation: false,
  alternateDelivery: emptyAlternate,
  customerPo: "",
  contactEmail: "",
  shippingInstructions: "",
  customerPickup: false,
  freightOption: "Standard",
  requestedArrivalDate: future(),
  lines: [],
};
function LegacyOrderEditor() {
  const { orderId } = useParams();
  const nav = useNavigate();
  const query = useQueryClient();
  const { data: accounts = [] } = useQuery<Account[]>({
    queryKey: ["accounts"],
    queryFn: () => api("/accounts"),
  });
  const { data: existing } = useQuery<Order>({
    queryKey: ["order", orderId],
    queryFn: () => api(`/orders/${orderId}`),
    enabled: !!orderId,
  });
  const [draft, setDraft] = useState<Draft>(blank);
  const [products, setProducts] = useState(false);
  const [toast, setToast] = useState("");
  useEffect(() => {
    if (existing)
      setDraft({
        ...existing,
        alternateDelivery: existing.alternateDelivery ?? emptyAlternate,
        customerPo: existing.customerPo ?? "",
        requestedArrivalDate: existing.requestedArrivalDate.slice(0, 10),
      });
  }, [existing]);
  const account = accounts.find((a) => a.id === draft.shipToAccountId);
  function choose(id: string) {
    const a = accounts.find((x) => x.id === id);
    setDraft((d) => ({
      ...d,
      shipToAccountId: id,
      contactEmail: a?.contactEmail ?? "",
      shippingInstructions: a?.shippingInstructions ?? "",
    }));
  }
  const save = useMutation({
    mutationFn: () =>
      api<Order>(orderId ? `/orders/${orderId}` : "/orders", {
        method: orderId ? "PUT" : "POST",
        body: JSON.stringify(draft),
      }),
    onSuccess: (o) => {
      query.invalidateQueries({ queryKey: ["orders"] });
      setToast(`Draft ${o.id} saved.`);
      if (!orderId)
        nav(`/crop-protection/orders/${o.id}/edit`, { replace: true });
    },
  });
  return (
    <>
      <PageTitle>
        CPP Order &gt; {orderId ? "Edit Order" : "Create Order"}
      </PageTitle>
      {toast && (
        <div role="status" className="toast">
          {toast}
        </div>
      )}
      <section className="panel form">
        <div className="notice">
          Prices and availability shown are demonstration data and refresh
          regularly. Orders after the configured cutoff may be delayed.
        </div>
        <div className="grid">
          <label>
            Sold To
            <input
              readOnly
              value={
                account?.soldToName ?? "Will display after selection of Ship To"
              }
            />
          </label>
          <label>
            Ship To *
            <select
              data-testid="ship-to"
              value={draft.shipToAccountId}
              onChange={(e) => choose(e.target.value)}
            >
              <option value="">Select authorized account</option>
              {accounts.map((a) => (
                <option value={a.id} key={a.id}>
                  {a.name} · {a.accountNumber}
                </option>
              ))}
            </select>
          </label>
          <label>
            Deliver To
            <select
              value={draft.deliverToAccountId ?? ""}
              onChange={(e) =>
                setDraft({ ...draft, deliverToAccountId: e.target.value })
              }
            >
              <option value="">Default delivery location</option>
              {accounts.map((a) => (
                <option value={a.id} key={a.id}>
                  {a.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Customer PO #{account?.requiresCustomerPo && " *"}
            <input
              value={draft.customerPo}
              onChange={(e) =>
                setDraft({ ...draft, customerPo: e.target.value })
              }
            />
          </label>
          <label className="wide">
            Shipping Instructions
            <textarea
              value={draft.shippingInstructions}
              onChange={(e) =>
                setDraft({ ...draft, shippingInstructions: e.target.value })
              }
            />
          </label>
          <label>
            Contact Email Address *
            <input
              type="email"
              value={draft.contactEmail}
              onChange={(e) =>
                setDraft({ ...draft, contactEmail: e.target.value })
              }
            />
          </label>
          <label>
            Freight Option
            <select
              value={draft.freightOption}
              onChange={(e) =>
                setDraft({ ...draft, freightOption: e.target.value })
              }
            >
              {[
                "Standard",
                "Customer Pickup",
                "Expedited",
                "Contract Freight",
              ].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
          <label>
            Requested Arrival Date
            <input
              type="date"
              min={new Date().toISOString().slice(0, 10)}
              value={draft.requestedArrivalDate}
              onChange={(e) =>
                setDraft({ ...draft, requestedArrivalDate: e.target.value })
              }
            />
          </label>
          <label className="check">
            <input
              type="checkbox"
              checked={draft.customerPickup}
              onChange={(e) =>
                setDraft({ ...draft, customerPickup: e.target.checked })
              }
            />{" "}
            Customer Pickup
          </label>
        </div>
        <div className="section-head">
          <h2>Selected Products</h2>
          <button
            data-testid="add-product"
            disabled={!draft.shipToAccountId}
            onClick={() => setProducts(true)}
          >
            <Plus size={17} /> Add Product
          </button>
        </div>
        {draft.lines.length ? (
          <Lines
            lines={draft.lines}
            onChange={(lines) => setDraft({ ...draft, lines })}
          />
        ) : (
          <Empty text="No products have been added. Select a Ship-To account, then add products." />
        )}
        <div className="footer">
          <b>
            Order subtotal:{" "}
            {money.format(
              draft.lines.reduce((n, l) => n + l.unitPrice * l.quantity, 0),
            )}
          </b>
          <div>
            <button onClick={() => save.mutate()} disabled={save.isPending}>
              Save Draft
            </button>
            <button
              className="primary"
              disabled={
                !orderId || !draft.shipToAccountId || draft.lines.length === 0
              }
              onClick={() => nav(`/crop-protection/orders/${orderId}/review`)}
            >
              Review Order
            </button>
          </div>
        </div>
      </section>
      {products && (
        <ProductPicker
          onClose={() => setProducts(false)}
          onAdd={(p) => {
            const next = [...draft.lines];
            p.forEach(({ product, quantity }) => {
              const i = next.findIndex((l) => l.productId === product.id);
              if (i >= 0)
                next[i] = { ...next[i], quantity: next[i].quantity + quantity };
              else
                next.push({
                  productId: product.id,
                  quantity,
                  uom: product.uom,
                  unitPrice: product.price,
                  requestedArrivalDate: draft.requestedArrivalDate,
                  inventorySource: "ASC",
                });
            });
            setDraft({ ...draft, lines: next });
            setProducts(false);
          }}
        />
      )}
    </>
  );
}
void LegacyOrderEditor;
function AccountCombobox({
  accounts,
  value,
  onSelect,
}: {
  accounts: Account[];
  value: string;
  onSelect: (id: string) => void;
}) {
  const [term, setTerm] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const chosen = accounts.find((a) => a.id === value);
  const results = accounts.filter((a) =>
    `${a.name} ${a.accountNumber} ${a.city} ${a.state} ${a.postalCode}`
      .toLowerCase()
      .includes(term.toLowerCase()),
  );
  function select(a: Account) {
    onSelect(a.id);
    setTerm("");
    setOpen(false);
  }
  return (
    <div className="combobox">
      <input
        data-testid="ship-to"
        role="combobox"
        aria-expanded={open}
        aria-controls="ship-to-options"
        value={
          open
            ? term
            : chosen
              ? `${chosen.name} · ${chosen.accountNumber}`
              : term
        }
        placeholder="Search account name, number, city, state or postal code"
        onFocus={() => {
          setOpen(true);
          setTerm("");
        }}
        onChange={(e) => {
          setTerm(e.target.value);
          setOpen(true);
          setActive(0);
        }}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive((i) => Math.min(i + 1, results.length - 1));
          }
          if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((i) => Math.max(i - 1, 0));
          }
          if (e.key === "Enter" && results[active]) {
            e.preventDefault();
            select(results[active]);
          }
          if (e.key === "Escape") setOpen(false);
        }}
      />
      {open && (
        <div id="ship-to-options" role="listbox" className="suggestions">
          {results.map((a, i) => (
            <button
              type="button"
              role="option"
              aria-selected={i === active}
              className={i === active ? "selected" : ""}
              key={a.id}
              onMouseDown={() => select(a)}
            >
              <b>
                {a.name} · {a.accountNumber}
              </b>
              <small>{a.address}</small>
            </button>
          ))}
          {!results.length && <p>No authorized accounts match your search.</p>}
        </div>
      )}
    </div>
  );
}
function OrderEditor() {
  const { orderId } = useParams();
  const nav = useNavigate();
  const query = useQueryClient();
  const {
    data: accounts = [],
    isError,
    refetch,
  } = useQuery<Account[]>({
    queryKey: ["accounts"],
    queryFn: () => api("/accounts"),
  });
  const { data: existing } = useQuery<Order>({
    queryKey: ["order", orderId],
    queryFn: () => api(`/orders/${orderId}`),
    enabled: !!orderId,
  });
  const [draft, setDraft] = useState<Draft>(blank);
  const [products, setProducts] = useState(false);
  const [toast, setToast] = useState("");
  const [attempted, setAttempted] = useState(false);
  useEffect(() => {
    if (existing)
      setDraft({
        ...blank,
        ...existing,
        alternateDelivery: existing.alternateDelivery ?? emptyAlternate,
        customerPo: existing.customerPo ?? "",
        requestedArrivalDate: existing.requestedArrivalDate.slice(0, 10),
      });
  }, [existing]);
  const account = accounts.find((a) => a.id === draft.shipToAccountId);
  const { data: deliverTos = [] } = useQuery<DeliverTo[]>({
    queryKey: ["deliver-to", draft.shipToAccountId],
    queryFn: () =>
      api(`/accounts/${draft.shipToAccountId}/deliver-to-locations`),
    enabled: !!draft.shipToAccountId,
  });
  useEffect(() => {
    if (
      draft.shipToAccountId &&
      !draft.deliverToAnotherLocation &&
      deliverTos.length &&
      !deliverTos.some((x) => x.id === draft.deliverToAccountId)
    )
      setDraft((d) => ({
        ...d,
        deliverToAccountId:
          deliverTos.find((x) => x.isDefault)?.id ?? deliverTos[0].id,
      }));
  }, [
    deliverTos,
    draft.shipToAccountId,
    draft.deliverToAccountId,
    draft.deliverToAnotherLocation,
  ]);
  function choose(id: string) {
    const a = accounts.find((x) => x.id === id);
    setDraft((d) => ({
      ...d,
      shipToAccountId: id,
      deliverToAccountId: undefined,
      deliverToAnotherLocation: false,
      alternateDelivery: emptyAlternate,
      contactEmail: a?.contactEmail ?? "",
      shippingInstructions: a?.shippingInstructions ?? "",
    }));
  }
  const save = useMutation({
    mutationFn: () =>
      api<Order>(orderId ? `/orders/${orderId}` : "/orders", {
        method: orderId ? "PUT" : "POST",
        body: JSON.stringify(draft),
      }),
    onSuccess: (o) => {
      query.invalidateQueries({ queryKey: ["orders"] });
      setToast(`Draft ${o.id} saved.`);
      if (!orderId)
        nav(`/crop-protection/orders/${o.id}/edit`, { replace: true });
    },
  });
  const altInvalid =
    draft.deliverToAnotherLocation &&
    (!draft.alternateDelivery.locationName ||
      !draft.alternateDelivery.addressLine1 ||
      !draft.alternateDelivery.city ||
      !draft.alternateDelivery.state ||
      !draft.alternateDelivery.postalCode);
  const canReview =
    !!orderId &&
    !!account &&
    draft.lines.length > 0 &&
    draft.contactEmail.includes("@") &&
    !altInvalid &&
    (!account.requiresCustomerPo || !!draft.customerPo.trim());
  const selectedDeliver = deliverTos.find(
    (x) => x.id === draft.deliverToAccountId,
  );
  return (
    <>
      <PageTitle>
        CPP Order &gt; {orderId ? "Edit Order" : "Create Order"}
      </PageTitle>
      {toast && (
        <div role="status" className="toast">
          {toast}
        </div>
      )}
      <section className="panel form">
        <div className="notice">
          Prices and availability are synthetic demonstration data. Account
          changes refresh delivery, pricing and availability context.
        </div>
        {isError ? (
          <Empty
            text="We could not load authorized accounts."
            action={<button onClick={() => refetch()}>Retry</button>}
          />
        ) : (
          <div className="grid">
            <label>
              Sold To
              <input
                readOnly
                value={
                  account?.soldToName ??
                  "Will display after selection of Ship To"
                }
              />
            </label>
            <label>
              Ship To *
              <AccountCombobox
                accounts={accounts}
                value={draft.shipToAccountId}
                onSelect={choose}
              />
              {account && <small>{account.address}</small>}
            </label>
            <label>
              Deliver To
              <select
                data-testid="deliver-to"
                disabled={!account || draft.deliverToAnotherLocation}
                value={draft.deliverToAccountId ?? ""}
                onChange={(e) =>
                  setDraft({ ...draft, deliverToAccountId: e.target.value })
                }
              >
                <option value="">Select delivery location</option>
                {deliverTos.map((x) => (
                  <option value={x.id} key={x.id}>
                    {x.name} · {x.accountNumber}
                  </option>
                ))}
              </select>
              {selectedDeliver && (
                <small>
                  {selectedDeliver.addressLine1}, {selectedDeliver.city},{" "}
                  {selectedDeliver.state} {selectedDeliver.postalCode}
                </small>
              )}
            </label>
            <label className="check wide">
              <input
                type="checkbox"
                disabled={!account}
                checked={draft.deliverToAnotherLocation}
                onChange={(e) =>
                  setDraft({
                    ...draft,
                    deliverToAnotherLocation: e.target.checked,
                    deliverToAccountId: e.target.checked
                      ? undefined
                      : (deliverTos.find((x) => x.isDefault)?.id ??
                        deliverTos[0]?.id),
                    alternateDelivery: e.target.checked
                      ? draft.alternateDelivery
                      : emptyAlternate,
                  })
                }
              />{" "}
              I want this delivered to another location.
            </label>
            {draft.deliverToAnotherLocation && (
              <fieldset className="alternate wide">
                <legend>Alternate delivery location</legend>
                {(
                  [
                    ["locationName", "Location Name"],
                    ["addressLine1", "Address Line 1"],
                    ["addressLine2", "Address Line 2"],
                    ["city", "City"],
                    ["state", "State"],
                    ["postalCode", "Postal Code"],
                    ["contactName", "Contact Name"],
                    ["contactPhone", "Contact Phone"],
                  ] as [keyof AlternateDelivery, string][]
                ).map(([key, label]) => (
                  <label key={key}>
                    {label}
                    {!["addressLine2", "contactName", "contactPhone"].includes(
                      key,
                    ) && " *"}
                    <input
                      value={draft.alternateDelivery[key]}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          alternateDelivery: {
                            ...draft.alternateDelivery,
                            [key]: e.target.value,
                          },
                        })
                      }
                    />
                  </label>
                ))}
                {attempted && altInvalid && (
                  <p className="error">
                    Complete required alternate delivery fields.
                  </p>
                )}
              </fieldset>
            )}
            <label>
              Customer PO #{account?.requiresCustomerPo && " *"}
              <input
                value={draft.customerPo}
                onChange={(e) =>
                  setDraft({ ...draft, customerPo: e.target.value })
                }
              />
            </label>
            <label className="wide">
              Shipping Instructions
              <textarea
                value={draft.shippingInstructions}
                onChange={(e) =>
                  setDraft({ ...draft, shippingInstructions: e.target.value })
                }
              />
            </label>
            <label>
              Contact Email Address *
              <input
                type="email"
                value={draft.contactEmail}
                onChange={(e) =>
                  setDraft({ ...draft, contactEmail: e.target.value })
                }
              />
            </label>
            <label>
              Freight Option
              <select
                value={draft.freightOption}
                onChange={(e) =>
                  setDraft({ ...draft, freightOption: e.target.value })
                }
              >
                {[
                  "Standard",
                  "Customer Pickup",
                  "Expedited",
                  "Contract Freight",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </label>
            <label>
              Requested Arrival Date *
              <input
                type="date"
                min={new Date().toISOString().slice(0, 10)}
                value={draft.requestedArrivalDate}
                onChange={(e) =>
                  setDraft({ ...draft, requestedArrivalDate: e.target.value })
                }
              />
            </label>
            <label className="check">
              <input
                type="checkbox"
                checked={draft.customerPickup}
                onChange={(e) =>
                  setDraft({ ...draft, customerPickup: e.target.checked })
                }
              />{" "}
              Customer Pickup
            </label>
          </div>
        )}
        <div className="section-head">
          <h2>Selected Products</h2>
          <button
            data-testid="add-product"
            disabled={!account}
            onClick={() => setProducts(true)}
          >
            <Plus size={17} /> Add Product
          </button>
        </div>
        {draft.lines.length ? (
          <Lines
            lines={draft.lines}
            onChange={(lines) => setDraft({ ...draft, lines })}
          />
        ) : (
          <Empty text="No products have been added. Select a Ship-To account, then add products." />
        )}
        <div className="footer">
          <b>
            Order subtotal:{" "}
            {money.format(
              draft.lines.reduce((n, l) => n + l.unitPrice * l.quantity, 0),
            )}
          </b>
          <div>
            <button
              onClick={() => save.mutate()}
              disabled={save.isPending || !account}
            >
              Save Draft
            </button>
            <button
              className="primary"
              onClick={() => {
                setAttempted(true);
                if (canReview) nav(`/crop-protection/orders/${orderId}/review`);
              }}
            >
              Review Order
            </button>
          </div>
        </div>
        {attempted && !canReview && (
          <p role="alert" className="error">
            Complete Ship-To, products, contact, delivery and required PO fields
            before review.
          </p>
        )}
      </section>
      {products && (
        <ProductPicker
          shipToAccountId={draft.shipToAccountId}
          onClose={() => setProducts(false)}
          onAdd={(items) => {
            const next = [...draft.lines];
            items.forEach(({ product, quantity }) => {
              const i = next.findIndex((l) => l.productId === product.id);
              if (i >= 0)
                next[i] = { ...next[i], quantity: next[i].quantity + quantity };
              else
                next.push({
                  productId: product.id,
                  quantity,
                  uom: product.uom,
                  unitPrice: product.price,
                  requestedArrivalDate: draft.requestedArrivalDate,
                  inventorySource: "ASC",
                });
            });
            setDraft({ ...draft, lines: next });
            setProducts(false);
            setToast("Products added to your order.");
          }}
        />
      )}
    </>
  );
}
function Lines({
  lines,
  onChange,
  readOnly = false,
}: {
  lines: Line[];
  onChange?: (l: Line[]) => void;
  readOnly?: boolean;
}) {
  const { data: products = [] } = useQuery<Product[]>({
    queryKey: ["all-products"],
    queryFn: () => api("/products/search"),
  });
  return (
    <Table>
      <thead>
        <tr>
          <th>Supplier</th>
          <th>Product / Item</th>
          <th>Inventory</th>
          <th>Quantity</th>
          <th>UOM</th>
          <th>Package</th>
          <th>Unit Price</th>
          <th>Line Total</th>
          {!readOnly && <th>Actions</th>}
        </tr>
      </thead>
      <tbody>
        {lines.map((l, i) => {
          const p = products.find((x) => x.id === l.productId);
          return (
            <tr key={l.productId}>
              <td>{p?.supplier}</td>
              <td>
                <b>{p?.name}</b>
                <small>{p?.itemNumber}</small>
              </td>
              <td>
                <Status value={p?.stoplightStatus ?? ""} />
              </td>
              <td>
                {readOnly ? (
                  l.quantity
                ) : (
                  <input
                    className="qty"
                    type="number"
                    min="1"
                    value={l.quantity}
                    onChange={(e) =>
                      onChange?.(
                        lines.map((x, j) =>
                          j === i ? { ...x, quantity: +e.target.value } : x,
                        ),
                      )
                    }
                  />
                )}
              </td>
              <td>{l.uom}</td>
              <td>{p?.packageSize}</td>
              <td>{money.format(l.unitPrice)}</td>
              <td>{money.format(l.unitPrice * l.quantity)}</td>
              {!readOnly && (
                <td>
                  <button
                    aria-label="Remove line"
                    onClick={() => onChange?.(lines.filter((_, j) => j !== i))}
                  >
                    <X size={16} />
                  </button>
                </td>
              )}
            </tr>
          );
        })}
      </tbody>
    </Table>
  );
}
function ProductPicker({
  shipToAccountId,
  onClose,
  onAdd,
}: {
  shipToAccountId?: string;
  onClose: () => void;
  onAdd: (x: { product: Product; quantity: number }[]) => void;
}) {
  const [criterion, setCriterion] = useState("Product Name");
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [favorites, setFavorites] = useState(false);
  const [selected, setSelected] = useState<Record<string, number>>({});
  const [suggestionQuery, setSuggestionQuery] = useState("");
  const [suggestionsOpen, setSuggestionsOpen] = useState(false);
  const [activeSuggestion, setActiveSuggestion] = useState(0);
  const pickerRef = useRef<HTMLDivElement>(null);
  const normalized = q.trim().replace(/\s+/g, " ");
  const valid = normalized.length >= 2;
  useEffect(() => {
    const timer = setTimeout(
      () => setSuggestionQuery(valid ? normalized : ""),
      250,
    );
    return () => clearTimeout(timer);
  }, [normalized, valid]);
  useEffect(() => {
    function outside(e: MouseEvent) {
      if (!pickerRef.current?.contains(e.target as Node))
        setSuggestionsOpen(false);
    }
    document.addEventListener("mousedown", outside);
    return () => document.removeEventListener("mousedown", outside);
  }, []);
  const suggestionResult = useQuery<{
    suggestions: {
      productId?: string;
      value: string;
      displayText: string;
      secondaryText?: string;
    }[];
  }>({
    queryKey: ["suggestions", criterion, suggestionQuery, shipToAccountId],
    queryFn: () =>
      api(
        `/products/suggestions?criterion=${encodeURIComponent(criterion)}&query=${encodeURIComponent(suggestionQuery)}&shipToAccountId=${encodeURIComponent(shipToAccountId ?? "")}`,
      ),
    enabled: suggestionQuery.length >= 2,
  });
  const suggestions = suggestionResult.data?.suggestions ?? [];
  const {
    data = [],
    isFetching,
    isError,
    refetch,
  } = useQuery<Product[]>({
    queryKey: ["products", criterion, search, favorites],
    queryFn: () =>
      api(
        `/products/search?criterion=${encodeURIComponent(criterion)}&query=${encodeURIComponent(search)}&shipToAccountId=${encodeURIComponent(shipToAccountId ?? "")}&favorites=${favorites}`,
      ),
    enabled: search.length > 0,
  });
  function run() {
    if (valid) {
      setSearch(normalized);
      setSuggestionsOpen(false);
    }
  }
  function chooseSuggestion(value: string) {
    setQ(value);
    setSearch(value);
    setSuggestionsOpen(false);
  }
  return (
    <div
      className="overlay"
      role="dialog"
      aria-modal="true"
      aria-label="Add products"
    >
      <div
        className="drawer"
        ref={pickerRef}
        onKeyDown={(e) => {
          if (e.key === "Escape" && !suggestionsOpen) onClose();
        }}
      >
        <div className="modal-head">
          <h2>Add products to your order:</h2>
          <button onClick={onClose}>
            Close <X size={18} />
          </button>
        </div>
        <div className="searchbar">
          <select
            value={criterion}
            onChange={(e) => setCriterion(e.target.value)}
          >
            {[
              "Product Name",
              "Item Number",
              "Active Ingredient",
              "Product Category",
              "GTIN",
              "Package Size",
              "Vendor/Supplier",
              "Product Line",
            ].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
          <input
            data-testid="product-search"
            value={q}
            role="combobox"
            aria-expanded={suggestionsOpen}
            aria-controls="product-suggestions"
            onChange={(e) => {
              setQ(e.target.value);
              setSuggestionsOpen(true);
              setActiveSuggestion(0);
            }}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setActiveSuggestion((i) =>
                  Math.min(i + 1, suggestions.length - 1),
                );
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setActiveSuggestion((i) => Math.max(i - 1, 0));
              } else if (e.key === "Enter") {
                e.preventDefault();
                if (suggestionsOpen && suggestions[activeSuggestion])
                  chooseSuggestion(suggestions[activeSuggestion].value);
                else run();
              } else if (e.key === "Escape") setSuggestionsOpen(false);
            }}
            placeholder="Enter search text"
          />
          {suggestionsOpen && valid && (
            <div
              id="product-suggestions"
              role="listbox"
              className="product-suggestions"
            >
              {suggestionResult.isFetching ? (
                <p>Loading suggestions…</p>
              ) : suggestions.length ? (
                suggestions.map((s, i) => (
                  <button
                    type="button"
                    role="option"
                    aria-selected={i === activeSuggestion}
                    className={i === activeSuggestion ? "selected" : ""}
                    key={`${s.productId}-${s.value}`}
                    onMouseDown={() => chooseSuggestion(s.value)}
                  >
                    <b>{s.displayText}</b>
                    <small>{s.secondaryText}</small>
                  </button>
                ))
              ) : (
                <p>No suggestions found.</p>
              )}
            </div>
          )}
          <button className="primary" disabled={!valid} onClick={run}>
            <Search size={16} /> Search
          </button>
        </div>
        {search && (
          <div className="chips">
            <span>
              {criterion}: {search}{" "}
              <button
                onClick={() => {
                  setSearch("");
                  setQ("");
                }}
              >
                ×
              </button>
            </span>
            <button
              onClick={() => {
                setSearch("");
                setQ("");
              }}
            >
              Clear All
            </button>
          </div>
        )}
        <div className="tabs">
          <button
            className={!favorites ? "active" : ""}
            onClick={() => setFavorites(false)}
          >
            All Products
          </button>
          <button
            className={favorites ? "active" : ""}
            onClick={() => setFavorites(true)}
          >
            Favorite Products
          </button>
        </div>
        <div className="results">
          {isFetching ? (
            <Skeleton />
          ) : isError ? (
            <Empty
              text="We could not load Crop Protection Products. Please retry or contact Customer Support."
              action={<button onClick={() => refetch()}>Retry</button>}
            />
          ) : search && data.length === 0 ? (
            <Empty text="No products matched. Try Product Name, Active Ingredient, Item Number, package size, or supplier. The support assistant can also help." />
          ) : (
            data.map((p) => (
              <div
                className={`product ${!p.orderable ? "disabled" : ""}`}
                key={p.id}
              >
                <input
                  type="checkbox"
                  aria-label={`Select ${p.name}`}
                  disabled={
                    !p.orderable || p.stoplightStatus === "No Availability"
                  }
                  checked={!!selected[p.id]}
                  onChange={(e) =>
                    setSelected((s) => {
                      const n = { ...s };
                      if (e.target.checked) n[p.id] = p.minimumQuantity;
                      else delete n[p.id];
                      return n;
                    })
                  }
                />
                <button aria-label="Toggle favorite">
                  <Star fill={p.favorite ? "#ffcc24" : "none"} size={18} />
                </button>
                <div className="product-main">
                  <b>{p.name}</b>
                  <small>
                    {p.supplier} · {p.itemNumber} · {p.packageSize}
                  </small>
                  <span>{p.activeIngredients}</span>
                </div>
                <Status value={p.stoplightStatus} />
                <b>
                  {money.format(p.price)}/{p.priceUom}
                </b>
                <div className="stepper">
                  <button
                    onClick={() =>
                      setSelected((s) => ({
                        ...s,
                        [p.id]: Math.max(
                          p.minimumQuantity,
                          (s[p.id] ?? 1) - p.quantityIncrement,
                        ),
                      }))
                    }
                  >
                    <Minus size={15} />
                  </button>
                  <input
                    aria-label={`${p.name} quantity`}
                    type="number"
                    disabled={!selected[p.id]}
                    min={p.minimumQuantity}
                    max={Math.min(p.maximumQuantity, p.availableInventory)}
                    step={p.quantityIncrement}
                    value={selected[p.id] ?? p.minimumQuantity}
                    onChange={(e) =>
                      setSelected((s) => ({
                        ...s,
                        [p.id]: Math.min(
                          p.availableInventory,
                          p.maximumQuantity,
                          Math.max(p.minimumQuantity, +e.target.value),
                        ),
                      }))
                    }
                  />
                  <button
                    onClick={() =>
                      setSelected((s) => ({
                        ...s,
                        [p.id]: Math.min(
                          p.availableInventory,
                          p.maximumQuantity,
                          (s[p.id] ?? p.minimumQuantity) + p.quantityIncrement,
                        ),
                      }))
                    }
                  >
                    <Plus size={15} />
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
        <div className="modal-footer">
          <b>{Object.keys(selected).length} product(s) selected</b>
          <button
            data-testid="confirm-products"
            className="primary"
            disabled={!Object.keys(selected).length}
            onClick={() =>
              onAdd(
                Object.entries(selected)
                  .map(([id, quantity]) => ({
                    product: data.find((p) => p.id === id)!,
                    quantity,
                  }))
                  .filter((x) => x.product),
              )
            }
          >
            Add Products to Order
          </button>
        </div>
      </div>
    </div>
  );
}
function Review({ readOnly = false }: { readOnly?: boolean }) {
  const { orderId } = useParams();
  const nav = useNavigate();
  const { data: o, isLoading } = useQuery<Order>({
    queryKey: ["order", orderId],
    queryFn: () => api(`/orders/${orderId}`),
  });
  const { data: accounts = [] } = useQuery<Account[]>({
    queryKey: ["accounts"],
    queryFn: () => api("/accounts"),
  });
  const validation = useQuery<Validation[]>({
    queryKey: ["validation", orderId],
    queryFn: () => api(`/orders/${orderId}/validate`, { method: "POST" }),
    enabled: !!o,
  });
  const submit = useMutation({
    mutationFn: () =>
      api<any>(`/orders/${orderId}/submit`, {
        method: "POST",
        headers: { "Idempotency-Key": crypto.randomUUID() },
      }),
    onSuccess: () => nav(`/crop-protection/orders/${orderId}/confirmation`),
  });
  if (isLoading || !o) return <Skeleton />;
  const a = accounts.find((x) => x.id === o.shipToAccountId);
  return (
    <>
      <PageTitle>Review CPP Order</PageTitle>
      <section className="panel">
        <div className="review-grid">
          <Info title="Sold-To and Ship-To">
            {o.soldToName}
            <br />
            <b>{a?.name}</b>
            <br />
            {a?.address}
          </Info>
          <Info title="Delivery & Freight">
            {o.freightOption}
            <br />
            Requested {new Date(o.requestedArrivalDate).toLocaleDateString()}
          </Info>
          <Info title="Contact & PO">
            {o.contactEmail}
            <br />
            PO: {o.customerPo || "Not supplied"}
          </Info>
          <Info title="Shipping Instructions">
            {o.shippingInstructions || "None"}
          </Info>
        </div>
        <h2>Products</h2>
        <Lines lines={o.lines} readOnly />
        <h2>Validation results</h2>
        {validation.data?.length === 0 ? (
          <p className="success">
            <CheckCircle2 /> All business rules passed.
          </p>
        ) : (
          validation.data?.map((v) => (
            <p key={v.code} className={v.severity}>
              <AlertTriangle /> <b>{v.severity.toUpperCase()}:</b> {v.message}
            </p>
          ))
        )}
        <div className="notice">
          Expected fulfillment grouping:{" "}
          {validation.data?.some((v) => v.code === "TRANSPORT_SPLIT")
            ? "Two or more transportation sub-orders"
            : "One fulfillment order"}
        </div>
        <div className="footer">
          <button
            onClick={() => nav(`/crop-protection/orders/${orderId}/edit`)}
            disabled={readOnly}
          >
            Edit Order
          </button>
          <button
            className="primary"
            disabled={
              readOnly ||
              submit.isPending ||
              validation.data?.some((v) => v.severity === "error")
            }
            onClick={() => submit.mutate()}
          >
            Confirm and Submit
          </button>
        </div>
        {submit.isError && (
          <p className="error" role="alert">
            Submission failed: {submit.error.message}. Your order remains
            recoverable.
          </p>
        )}
      </section>
    </>
  );
}
function Confirmation() {
  const { orderId } = useParams();
  const { data: o } = useQuery<Order>({
    queryKey: ["order", orderId],
    queryFn: () => api(`/orders/${orderId}`),
  });
  const { data: accounts = [] } = useQuery<Account[]>({
    queryKey: ["accounts"],
    queryFn: () => api("/accounts"),
  });
  if (!o) return <Skeleton />;
  const a = accounts.find((x) => x.id === o.shipToAccountId);
  return (
    <>
      <PageTitle>Order Confirmation</PageTitle>
      <section className="panel confirmation">
        <CheckCircle2 size={54} />
        <h2>Your CPP order was submitted successfully</h2>
        <div className="web-number">{o.webOrderNumber}</div>
        <p>
          Submitted {o.status === "Submitted" ? "successfully" : "—"} · Ship-To{" "}
          {a?.name}
        </p>
        <Lines lines={o.lines} readOnly />
        <div className="actions">
          <a
            className="button primary"
            href={`${API}/orders/${o.id}/confirmation.pdf`}
          >
            <Download size={16} /> Download Confirmation PDF
          </a>
          <button onClick={() => window.print()}>
            <Printer size={16} /> Print
          </button>
          <Link className="button" to={`/crop-protection/orders/${o.id}`}>
            View Order
          </Link>
          <Link className="button" to="/crop-protection/orders/new">
            Create Another Order
          </Link>
          <Link className="button" to="/crop-protection/orders">
            Return to Orders
          </Link>
        </div>
      </section>
    </>
  );
}
function Assistant() {
  const location = useLocation();
  const nav = useNavigate();
  const query = useQueryClient();
  const chatLogRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [catalogChoiceMode, setCatalogChoiceMode] = useState<
    "activeIngredient" | "productName" | null
  >(null);
  const [conversationId, setConversationId] = useState<string | undefined>();
  const [contextEntities, setContextEntities] = useState<
    Record<string, string | null>
  >({});
  const [contextOrderLines, setContextOrderLines] = useState<
    AssistantOrderLine[]
  >([]);
  const [messages, setMessages] = useState<
    {
      role: "assistant" | "user";
      text: string;
      products?: Product[];
      status?: string;
      missingFields?: AssistantMissingField[];
      clarificationQuestions?: string[];
      toolCalls?: AssistantToolCall[];
      intent?: string;
      searchQuery?: string;
      searchSummary?: string;
      entities?: Record<string, string | null>;
      orderLines?: AssistantOrderLine[];
      grounding?: AssistantResponse["grounding"];
      traceId?: string;
    }[]
  >([
    {
      role: "assistant",
      text: assistantWelcomeMessage,
    },
  ]);
  const [text, setText] = useState("");
  const orderIdMatch = location.pathname.match(
    /\/crop-protection\/orders\/([^/]+)/,
  );
  const orderId = orderIdMatch?.[1];
  const catalog = useQuery({
    queryKey: ["assistant-product-catalog"],
    queryFn: () => api<Product[]>("/products/search?q="),
    enabled: open && catalogChoiceMode !== null,
    staleTime: 60_000,
  });
  const activeIngredientChoices = Array.from(
    new Set(
      (catalog.data ?? [])
        .flatMap((product) => product.activeIngredients.split(/[,;]/))
        .map((ingredient) => ingredient.trim())
        .filter(Boolean),
    ),
  ).sort((left, right) => left.localeCompare(right));
  const productChoices = [...(catalog.data ?? [])].sort((left, right) =>
    left.name.localeCompare(right.name),
  );

  useEffect(() => {
    if (!open) return;
    const frame = requestAnimationFrame(() => {
      const chatLog = chatLogRef.current;
      chatLog?.scrollTo({ top: chatLog.scrollHeight, behavior: "smooth" });
    });
    return () => cancelAnimationFrame(frame);
  }, [messages, open, catalogChoiceMode, catalog.data]);

  const send = useMutation({
    mutationFn: (message: string) => {
      const request: AssistantRequest = {
        message,
        conversationId,
        orderId,
        history: messages.map((m) => ({ role: m.role, text: m.text })),
        contextEntities,
        contextOrderLines,
      };
      return api<AssistantResponse>("/agent/messages", {
        method: "POST",
        body: JSON.stringify(request),
      });
    },
    onSuccess: (r) => {
      setConversationId(r.conversationId);
      if (r.status === "Complete" && r.intent === "SubmitOrder") {
        query.invalidateQueries({ queryKey: ["orders"] });
        setContextEntities({});
        setContextOrderLines([]);
      } else {
        setContextEntities(r.entities);
        setContextOrderLines(r.orderLines ?? []);
      }
      setMessages((m) => [
        ...m,
        {
          role: "assistant",
          text: r.reply,
          products: r.products,
          status: r.status,
          missingFields: r.missingFields,
          clarificationQuestions: r.clarificationQuestions,
          toolCalls: r.toolCalls,
          intent: r.intent,
          searchQuery: r.searchQuery,
          searchSummary: r.searchSummary,
          entities: r.entities,
          orderLines: r.orderLines,
          grounding: r.grounding,
          traceId: r.trace.traceId,
        },
      ]);
    },
  });

  const editDraft = useMutation({
    mutationFn: async ({
      entities,
      orderLines,
    }: {
      entities: Record<string, string | null>;
      orderLines: AssistantOrderLine[];
    }) => {
      const [accounts, products] = await Promise.all([
        api<Account[]>("/accounts"),
        api<Product[]>("/products/search?q="),
      ]);
      const normalize = (value?: string | null) =>
        (value ?? "").replace(/[^a-z0-9]/gi, "").toLowerCase();
      const accountReference = firstEntity(
        entities,
        "shipToAccountId",
        "accountId",
        "shipToAccountName",
        "accountName",
        "account",
      );
      const account = accounts.find(
        (candidate) =>
          candidate.id === accountReference ||
          normalize(candidate.name) === normalize(accountReference) ||
          normalize(candidate.accountNumber) === normalize(accountReference),
      );
      if (!account)
        throw new Error("The Ship-To account could not be resolved.");

      const deliveryLocations = await api<DeliverTo[]>(
        `/accounts/${account.id}/deliver-to-locations`,
      );
      const deliveryReference = firstEntity(
        entities,
        "deliverToAccountId",
        "deliverToId",
        "deliverToName",
        "deliveryLocation",
        "deliverTo",
      );
      const delivery =
        deliveryLocations.find(
          (candidate) =>
            candidate.id === deliveryReference ||
            normalize(candidate.name) === normalize(deliveryReference),
        ) ?? deliveryLocations.find((candidate) => candidate.isDefault);

      const lines = orderLines.map((line) => {
        const product = products.find(
          (candidate) =>
            candidate.id === line.productId ||
            normalize(candidate.itemNumber) === normalize(line.itemNumber) ||
            normalize(candidate.name) === normalize(line.productName),
        );
        if (!product)
          throw new Error(
            `The product ${line.productName ?? line.itemNumber ?? "in the order"} could not be resolved.`,
          );
        return {
          productId: product.id,
          quantity: line.quantity,
          uom: product.uom,
          unitPrice: product.price,
          requestedArrivalDate:
            firstEntity(entities, "requestedArrivalDate", "deliveryDate") ??
            future(),
          inventorySource: "ASC",
        } satisfies Line;
      });
      if (!lines.length) throw new Error("The order has no products to edit.");

      return api<Order>("/orders", {
        method: "POST",
        body: JSON.stringify({
          ...blank,
          shipToAccountId: account.id,
          deliverToAccountId: delivery?.id,
          customerPo:
            firstEntity(entities, "customerPo", "poNumber", "po") ?? "",
          contactEmail: account.contactEmail,
          shippingInstructions: account.shippingInstructions,
          requestedArrivalDate:
            firstEntity(entities, "requestedArrivalDate", "deliveryDate") ??
            future(),
          lines,
        }),
      });
    },
    onSuccess: (order) => {
      query.invalidateQueries({ queryKey: ["orders"] });
      setOpen(false);
      nav(`/crop-protection/orders/${order.id}/edit`);
    },
  });

  function go(v = text) {
    if (!v.trim()) return;
    setMessages((m) => [...m, { role: "user", text: v }]);
    send.mutate(v);
    setText("");
  }

  function clearChat() {
    setConversationId(undefined);
    setContextEntities({});
    setContextOrderLines([]);
    setMessages([{ role: "assistant", text: assistantWelcomeMessage }]);
    setCatalogChoiceMode(null);
    setText("");
    send.reset();
  }

  return (
    <div className="chat">
      <button
        aria-label="Open CPP assistant"
        className="chat-fab"
        onClick={() => setOpen(!open)}
      >
        <MessageCircle />
      </button>
      {open && (
        <section className="chat-panel">
          <div className="modal-head">
            <b>CPP Order Assistant</b>
            <div className="chat-head-actions">
              <button
                className="chat-clear"
                disabled={send.isPending || messages.length === 1}
                onClick={clearChat}
                title="Start a new conversation"
              >
                <RotateCcw size={14} /> Clear chat
              </button>
              <button
                aria-label="Close CPP assistant"
                onClick={() => setOpen(false)}
              >
                <X size={17} />
              </button>
            </div>
          </div>
          <div className="chat-log" ref={chatLogRef}>
            {messages.map((m, i) => (
              <div className={m.role} key={i}>
                {m.intent === "FindProduct" ? (
                  m.searchSummary ? (
                    <p className="assistant-search-summary">
                      {m.searchSummary}
                    </p>
                  ) : null
                ) : (
                  m.text
                )}
                {m.intent !== "FindProduct" &&
                  !!m.clarificationQuestions?.length && (
                    <div className="assistant-block">
                      <b>Needed to continue</b>
                      {m.clarificationQuestions.map((q) => (
                        <small key={q}>{q}</small>
                      ))}
                    </div>
                  )}
                {m.intent === "FindProduct" &&
                  m.products?.map((p) => (
                    <button
                      type="button"
                      className="sku"
                      key={p.id}
                      disabled={send.isPending}
                      aria-label={`Select ${p.name}, item ${p.itemNumber}`}
                      onClick={() =>
                        go(`I want to order ${p.name} (Item #${p.itemNumber}).`)
                      }
                    >
                      <div className="sku-head">
                        <b>{p.name}</b>
                        <span className="sku-head-actions">
                          <span
                            className={`sku-status ${p.stoplightStatus.toLowerCase()}`}
                          >
                            {p.stoplightStatus}
                          </span>
                          <ChevronRight size={16} aria-hidden="true" />
                        </span>
                      </div>
                      <small>
                        {p.itemNumber} · {p.packageSize}
                      </small>
                      <small className="sku-ingredient">
                        Active ingredient: {p.activeIngredients}
                      </small>
                    </button>
                  ))}
                {m.entities &&
                  m.status !== "Complete" &&
                  hasOrderReviewDetails(m.entities, m.orderLines) && (
                    <AssistantOrderReview
                      entities={m.entities}
                      orderLines={m.orderLines ?? []}
                      missingFields={m.missingFields ?? []}
                      hasOutstandingQuestions={Boolean(
                        m.clarificationQuestions?.length,
                      )}
                      busy={send.isPending || editDraft.isPending}
                      onConfirm={() =>
                        go(
                          "Confirm and submit this order using the reviewed details.",
                        )
                      }
                      onEdit={() =>
                        editDraft.mutate({
                          entities: m.entities ?? {},
                          orderLines: m.orderLines ?? [],
                        })
                      }
                      onAddProduct={() =>
                        go("I want to add another product to this order.")
                      }
                      onQuantityChange={(lineIndex, quantity) => {
                        const currentLines = m.orderLines?.length
                          ? m.orderLines
                          : [
                              {
                                productId: m.entities?.productId,
                                itemNumber: m.entities?.itemNumber,
                                productName: m.entities?.productName,
                                quantity: Number(m.entities?.quantity),
                              },
                            ];
                        const nextLines = currentLines
                          .map((line, index) =>
                            index === lineIndex ? { ...line, quantity } : line,
                          )
                          .filter((line) => line.quantity > 0);
                        const singleLine =
                          nextLines.length === 1 ? nextLines[0] : undefined;
                        const nextEntities = {
                          ...(m.entities ?? {}),
                          quantity: singleLine
                            ? String(singleLine.quantity)
                            : null,
                          productId: singleLine?.productId ?? null,
                          itemNumber: singleLine?.itemNumber ?? null,
                          productName: singleLine?.productName ?? null,
                        };
                        setMessages((current) =>
                          current.map((message, messageIndex) =>
                            messageIndex === i
                              ? {
                                  ...message,
                                  entities: nextEntities,
                                  orderLines: nextLines,
                                }
                              : message,
                          ),
                        );
                        setContextEntities(nextEntities);
                        setContextOrderLines(nextLines);
                      }}
                    />
                  )}
                {m.status === "Complete" && m.intent === "SubmitOrder" && (
                  <AssistantOrderConfirmation
                    text={m.text}
                    grounding={m.grounding ?? []}
                    onNewOrder={() => {
                      setContextEntities({});
                      setContextOrderLines([]);
                      go("Start a new CPP order.");
                    }}
                  />
                )}
              </div>
            ))}
            {messages.length === 1 && !catalogChoiceMode && (
              <section
                className="assistant-starters"
                aria-label="Starter questions"
              >
                <div className="assistant-starters-head">
                  <b>Try asking</b>
                  <small>Choose an example for a quick result</small>
                </div>
                <div className="assistant-starter-grid">
                  {assistantStarters.map((starter) => (
                    <button
                      key={starter.label}
                      disabled={send.isPending}
                      onClick={() =>
                        "mode" in starter
                          ? setCatalogChoiceMode(starter.mode ?? null)
                          : go(starter.prompt)
                      }
                    >
                      <span
                        className={`assistant-starter-icon ${starter.kind}`}
                      >
                        {starter.kind === "search" ? (
                          <Search size={16} />
                        ) : (
                          <ShoppingCart size={16} />
                        )}
                      </span>
                      <span>
                        <b>{starter.label}</b>
                        <small>{starter.detail}</small>
                      </span>
                    </button>
                  ))}
                </div>
              </section>
            )}
            {messages.length === 1 && catalogChoiceMode && (
              <section
                className="assistant-catalog-picker"
                aria-label={
                  catalogChoiceMode === "activeIngredient"
                    ? "Choose an active ingredient"
                    : "Choose a product"
                }
              >
                <div className="assistant-picker-head">
                  <div>
                    <b>
                      {catalogChoiceMode === "activeIngredient"
                        ? "Choose an active ingredient"
                        : "Choose a product"}
                    </b>
                    <small>
                      {catalogChoiceMode === "activeIngredient"
                        ? "Select an ingredient to see matching products"
                        : "Select a product to start an order"}
                    </small>
                  </div>
                  <button onClick={() => setCatalogChoiceMode(null)}>
                    Back
                  </button>
                </div>
                {catalog.isPending && <small>Loading catalog…</small>}
                {catalog.isError && (
                  <small>Unable to load the catalog. Please try again.</small>
                )}
                {!catalog.isPending && !catalog.isError && (
                  <div className="assistant-picker-list">
                    {catalogChoiceMode === "activeIngredient"
                      ? activeIngredientChoices.map((ingredient) => (
                          <button
                            key={ingredient}
                            onClick={() => {
                              setCatalogChoiceMode(null);
                              go(
                                `Find products with the active ingredient ${ingredient}.`,
                              );
                            }}
                          >
                            <span>
                              <b>{ingredient}</b>
                              <small>View matching products</small>
                            </span>
                            <ChevronRight size={16} />
                          </button>
                        ))
                      : productChoices.map((product) => (
                          <button
                            key={product.id}
                            onClick={() => {
                              setCatalogChoiceMode(null);
                              go(
                                `I want to order ${product.name} (Item #${product.itemNumber}).`,
                              );
                            }}
                          >
                            <span>
                              <b>{product.name}</b>
                              <small>
                                {product.itemNumber} · {product.packageSize}
                              </small>
                            </span>
                            <ChevronRight size={16} />
                          </button>
                        ))}
                  </div>
                )}
              </section>
            )}
          </div>
          {editDraft.isError && (
            <p className="error assistant-edit-error" role="alert">
              {editDraft.error.message}
            </p>
          )}
          <div className="inline">
            <input
              aria-label="Assistant message"
              value={text}
              onChange={(e) => setText(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && go()}
              placeholder="Type your request…"
            />
            <button onClick={() => go()}>Send</button>
          </div>
        </section>
      )}
    </div>
  );
}

function firstEntity(
  entities: Record<string, string | null>,
  ...keys: string[]
) {
  for (const key of keys) {
    const value = entities[key];
    if (value?.trim()) return value.trim();
  }
  return undefined;
}

function hasOrderReviewDetails(
  entities: Record<string, string | null>,
  orderLines?: AssistantOrderLine[],
) {
  const product = firstEntity(
    entities,
    "productName",
    "itemNumber",
    "productId",
  );
  const quantity = firstEntity(entities, "quantity");
  const account = firstEntity(entities, "shipToAccountName", "shipToAccountId");
  const hasLines = Boolean(orderLines?.some((line) => line.quantity > 0));
  return Boolean(account && (hasLines || (product && quantity)));
}

function AssistantOrderReview({
  entities,
  orderLines,
  missingFields,
  hasOutstandingQuestions,
  busy,
  onConfirm,
  onEdit,
  onAddProduct,
  onQuantityChange,
}: {
  entities: Record<string, string | null>;
  orderLines: AssistantOrderLine[];
  missingFields: AssistantMissingField[];
  hasOutstandingQuestions: boolean;
  busy: boolean;
  onConfirm: () => void;
  onEdit: () => void;
  onAddProduct: () => void;
  onQuantityChange: (lineIndex: number, quantity: number) => void;
}) {
  const product = firstEntity(
    entities,
    "productName",
    "itemNumber",
    "productId",
  );
  const itemNumber = firstEntity(entities, "itemNumber");
  const quantity = firstEntity(entities, "quantity");
  const account = firstEntity(entities, "shipToAccountName", "shipToAccountId");
  const delivery = firstEntity(entities, "deliverToName", "deliverToId");
  const customerPo = firstEntity(entities, "customerPo");
  const reviewLines = orderLines.length
    ? orderLines
    : [
        {
          productName: product,
          itemNumber,
          quantity: Number(quantity),
        },
      ];
  const ready =
    missingFields.length === 0 &&
    !hasOutstandingQuestions &&
    Boolean(delivery) &&
    reviewLines.every(
      (line) =>
        line.quantity > 0 &&
        Boolean(line.productName || line.itemNumber || line.productId),
    );

  return (
    <section className="assistant-order-card" aria-label="Order review">
      <div className="assistant-order-head">
        <span className="assistant-order-icon">
          <ShoppingCart size={17} />
        </span>
        <div>
          <small>Review before submission</small>
          <b>Order details captured</b>
        </div>
        <span className={`assistant-ready ${ready ? "ready" : "incomplete"}`}>
          {ready ? "Ready" : "Needs details"}
        </span>
      </div>
      <div className="assistant-order-account">
        <Truck size={15} />
        <span>
          <small>Ship-to account</small>
          <b>{account}</b>
        </span>
      </div>
      <div className="assistant-order-lines">
        {reviewLines.map((line, index) => {
          const lineName =
            line.productName || line.itemNumber || line.productId;
          return (
            <div
              className="assistant-order-line"
              key={`${line.productId ?? line.itemNumber ?? lineName}-${index}`}
            >
              <div>
                <small>
                  {reviewLines.length > 1 ? `Product ${index + 1}` : "Product"}
                </small>
                <b>{lineName}</b>
                {line.itemNumber && lineName !== line.itemNumber && (
                  <em>#{line.itemNumber}</em>
                )}
              </div>
              <div className="assistant-quantity-control">
                <small>Quantity</small>
                <span>
                  <button
                    type="button"
                    disabled={busy}
                    aria-label={
                      line.quantity === 1
                        ? `Remove ${lineName}`
                        : `Decrease quantity of ${lineName}`
                    }
                    title={line.quantity === 1 ? "Remove product" : "Decrease"}
                    onClick={() => onQuantityChange(index, line.quantity - 1)}
                  >
                    <Minus size={13} />
                  </button>
                  <b aria-label={`Quantity ${line.quantity}`}>
                    {line.quantity}
                  </b>
                  <button
                    type="button"
                    disabled={busy}
                    aria-label={`Increase quantity of ${lineName}`}
                    title="Increase"
                    onClick={() => onQuantityChange(index, line.quantity + 1)}
                  >
                    <Plus size={13} />
                  </button>
                </span>
              </div>
            </div>
          );
        })}
      </div>
      <dl className="assistant-order-facts">
        <div>
          <dt>Delivery</dt>
          <dd>{delivery ?? "Not selected"}</dd>
        </div>
        {customerPo && (
          <div>
            <dt>Customer PO</dt>
            <dd>{customerPo}</dd>
          </div>
        )}
      </dl>
      {!ready && (
        <p className="assistant-order-note">
          Complete the outstanding details before submitting this order.
        </p>
      )}
      <div className="assistant-order-buttons">
        <button
          className="primary"
          disabled={!ready || busy}
          onClick={onConfirm}
        >
          <CheckCircle2 size={15} /> Confirm order
        </button>
        <button disabled={busy} onClick={onEdit}>
          <Pencil size={15} /> Edit
        </button>
        <button disabled={busy} onClick={onAddProduct}>
          <Plus size={15} /> Add product
        </button>
      </div>
    </section>
  );
}

function AssistantOrderConfirmation({
  text,
  grounding,
  onNewOrder,
}: {
  text: string;
  grounding: AssistantResponse["grounding"];
  onNewOrder: () => void;
}) {
  const webOrder = text.match(/WEB-[A-Z0-9-]+/i)?.[0];
  const orderId = grounding.find((item) => item.source === "order")?.identifier;

  return (
    <section className="assistant-confirm-card" aria-label="Order confirmed">
      <div className="assistant-confirm-mark">
        <CheckCircle2 size={21} />
      </div>
      <div className="assistant-confirm-copy">
        <small>Submission complete</small>
        <b>Order confirmed</b>
        <span>{webOrder ?? "Confirmation created"}</span>
      </div>
      <div className="assistant-confirm-actions">
        {orderId && (
          <>
            <Link to={`/crop-protection/orders/${orderId}`}>
              <FileText size={15} /> View order
            </Link>
            <a href={`${API}/orders/${orderId}/confirmation.pdf`} download>
              <Download size={15} /> Export PDF
            </a>
          </>
        )}
        <Link to="/crop-protection/orders">
          <Truck size={15} /> Track orders
        </Link>
        <button onClick={onNewOrder}>
          <RotateCcw size={15} /> New order
        </button>
      </div>
    </section>
  );
}
function Section() {
  const { sectionName } = useParams();
  return (
    <>
      <PageTitle>{sectionName}</PageTitle>
      <section className="panel">
        <Empty
          text="This section is outside the current CPP proof of concept. Its route is available so navigation remains complete."
          action={
            <Link className="button" to="/crop-protection/orders">
              Go to CPP Orders
            </Link>
          }
        />
      </section>
    </>
  );
}
function Table({ children }: { children: React.ReactNode }) {
  return (
    <div className="table-wrap">
      <table>{children}</table>
    </div>
  );
}
function Status({ value }: { value: string }) {
  const c =
    value.toLowerCase().includes("no") || value.toLowerCase().includes("fail")
      ? "red"
      : value.toLowerCase().includes("limited") || value === "Draft"
        ? "yellow"
        : "green";
  return (
    <span className={`status ${c}`}>
      <i />
      {value}
    </span>
  );
}
function Empty({ text, action }: { text: string; action?: React.ReactNode }) {
  return (
    <div className="empty">
      <Package />
      <p>{text}</p>
      {action}
    </div>
  );
}
function Skeleton() {
  return (
    <div className="skeleton">
      <i />
      <i />
      <i />
      <i />
    </div>
  );
}
function Info({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section>
      <h3>{title}</h3>
      <p>{children}</p>
    </section>
  );
}
export default App;
