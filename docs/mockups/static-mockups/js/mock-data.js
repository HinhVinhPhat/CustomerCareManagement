// Sample data matching 03_SampleData.sql. Later replaced by fetch('/api/v1/...').
const EMPLOYEE_NAMES = {1:'John Smith',2:'Mary Johnson',3:'David Brown',4:'Susan Davis',7:'Kevin Taylor',8:'Helen Anderson'};
const USERS = [ // demo password for every account: 123456
  {username:'john', employeeID:1, role:'Manager'},{username:'susan', employeeID:4, role:'Manager'},
  {username:'mary', employeeID:2, role:'Sales'},{username:'kevin', employeeID:7, role:'Sales'},
  {username:'david', employeeID:3, role:'Support'},{username:'helen', employeeID:8, role:'Support'}];
const CUSTOMERS = [
  {id:1,name:'ABC Company Ltd',customerType:'Business',email:'abc@company.com',phone:'0281111111',tier:'Diamond',ownerEmployeeID:1,totalValue:128e6},
  {id:2,name:'XYZ Corporation',customerType:'Business',email:'xyz@company.com',phone:'0282222222',tier:'Silver',ownerEmployeeID:2,totalValue:48e6},
  {id:3,name:'Emily Clark',customerType:'Individual',email:'emily.clark@mail.com',phone:'0911111111',tier:'Silver',ownerEmployeeID:2,totalValue:20e6},
  {id:4,name:'Daniel Harris',customerType:'Individual',email:'daniel.harris@mail.com',phone:'0922222222',tier:'Standard',ownerEmployeeID:7,totalValue:5e6},
  {id:5,name:'DEF Company Ltd',customerType:'Business',email:'def@company.com',phone:'0283333333',tier:'Diamond',ownerEmployeeID:7,totalValue:100e6},
  {id:6,name:'Sophia Lewis',customerType:'Individual',email:'sophia.lewis@mail.com',phone:'0933333333',tier:'Standard',ownerEmployeeID:2,totalValue:0},
  {id:7,name:'GHI Corporation',customerType:'Business',email:'ghi@company.com',phone:'0284444444',tier:'Diamond',ownerEmployeeID:1,totalValue:128e6},
  {id:8,name:'Matthew Walker',customerType:'Individual',email:'matthew.walker@mail.com',phone:'0944444444',tier:'Silver',ownerEmployeeID:2,totalValue:20e6},
  {id:9,name:'JKL Company Ltd',customerType:'Business',email:'jkl@company.com',phone:'0285555555',tier:'Standard',ownerEmployeeID:7,totalValue:12e6},
  {id:10,name:'Olivia Young',customerType:'Individual',email:'olivia.young@mail.com',phone:'0955555555',tier:'Standard',ownerEmployeeID:2,totalValue:13e6}];
const DASH = {
  summary:{customers:10,newCustomers30Days:1,activeContracts:11,expiringContracts:2,openFeedback:5,overdueFeedback:2,revenueThisYear:186e6,avgSatisfaction:4.3},
  revenueByMonth:[12,0,25,18,30,0,45,20,35,25,40,0], // million VND
  expiringList:[{contractID:3,customer:'ABC Company Ltd',daysLeft:15,totalValue:80e6},
                {contractID:10,customer:'GHI Corporation',daysLeft:25,totalValue:60e6}],
  feedbackList:[{customer:'XYZ Corporation',content:'Have not received the e-invoice',priority:1,status:'New',overdue:false},
                {customer:'XYZ Corporation',content:'The interface should be friendlier',priority:3,status:'In progress',overdue:true},
                {customer:'Emily Clark',content:'Suggest adding an automatic reminder feature',priority:2,status:'In progress',overdue:true},
                {customer:'Matthew Walker',content:'HRM runs slowly on low-end machines',priority:2,status:'In progress',overdue:false}],
  scheduleList:[{customer:'DEF Company Ltd',careType:'Renewal',status:'Overdue',date:'3 days ago'},
                {customer:'GHI Corporation',careType:'Renewal',status:'Today',date:'Today'},
                {customer:'ABC Company Ltd',careType:'Renewal reminder',status:'Upcoming',date:'In 2 days'},
                {customer:'XYZ Corporation',careType:'Survey',status:'Upcoming',date:'In 3 days'}]
};
