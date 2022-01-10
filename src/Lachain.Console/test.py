from jsonrpcclient import request
import requests

endpoint = "http://localhost:7070"

if __name__ == '__main__':

    data = request("clearInMemoryPool", params={})
    # this is public key
    data["key"] = "<RSAKeyValue><Modulus>zhGq18RvM1Xwkqd28Bei/oXNrq+GaGLFWYIOxUibDMLLyS+YbhsBEOVcArGtZe96ZLVbcippCYTsKcNkHYcxihpZcYBkA/WKrW0+RtuJiaqVVA7jv7S5oHTn8k1DHXu5Zo4nJAKD0e4cDRvFezBKjdJHU=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"
    print(data)
    headers={}
    
    response = requests.post(endpoint, headers=headers, json=data)
    print(response.json())