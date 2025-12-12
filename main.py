import requests
import json

url = "https://api.hellobike.com/auth"

# 1. 发送验证码
payload_send = {
    "mobile": "19109838053",
    "action": "user.account.sendCodeV2"
}

resp_send = requests.post(url, data=json.dumps(payload_send), headers={
    "Content-Type": "application/json"
})

print("发送验证码响应：")
print(resp_send.text)

code = input("请输入收到的验证码：")
# 2. 登录
payload_login = {
    "mobile": "18771506573",
    "code": code,
    "action": "user.account.login"
}

resp_login = requests.post(url, data=json.dumps(payload_login), headers={
    "Content-Type": "application/json"
})

print("\n登录响应：")
print(resp_login.text)
